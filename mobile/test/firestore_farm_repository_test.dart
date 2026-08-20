import 'dart:async';

import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:fake_cloud_firestore/fake_cloud_firestore.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/models/faaliyet.dart';
import 'package:mobile/models/tarla.dart';
import 'package:mobile/services/firestore_farm_repository.dart';

void main() {
  test('writes the exact owner-scoped farm schema', () async {
    final store = _RecordingStore();
    final repository = FirestoreFarmRepository(uid: 'uid-1', store: store);
    await repository.createFarm(_farm());

    expect(store.path, 'farms/f1');
    expect(store.data.keys, unorderedEquals(FirestoreFarmContract.farmFields));
    expect(store.data['ownerId'], 'uid-1');
    expect(store.data['sizeInHectares'], 12.5);
    expect(store.data['createdAt'], isA<FirestoreServerTimestamp>());
    expect(store.data['updatedAt'], isA<FirestoreServerTimestamp>());
  });

  test('writes activity under its farm with description and voice provenance', () async {
    final store = _RecordingStore();
    final repository = FirestoreFarmRepository(uid: 'uid-1', store: store);
    await repository.createActivity('f1', _activity(inputMethod: 'VOICE'));

    expect(store.path, 'farms/f1/activities/a1');
    expect(store.data.keys, unorderedEquals(FirestoreFarmContract.activityFields));
    expect(store.data['activityType'], 'OTHER');
    expect(store.data['description'], 'Damlama sulama yapıldı.');
    expect(store.data['inputMethod'], 'VOICE');
  });

  test('passes owner scope to the store query', () async {
    final store = _RecordingStore();
    final repository = FirestoreFarmRepository(uid: 'uid-1', store: store);
    store.farms = [FirestoreFarmDocument(id: 'f1', data: _farmData())];

    await repository.watchFarms('uid-1').first;

    expect(store.watchedOwnerId, 'uid-1');
  });

  test('decodes Firestore timestamps and retains activity description and input method', () async {
    final store = _RecordingStore();
    final repository = FirestoreFarmRepository(uid: 'uid-1', store: store);
    store.activities = [
      FirestoreActivityDocument(
        id: 'a1',
        data: {
          'activityType': 'OTHER',
          'description': 'Damlama sulama yapıldı.',
          'occurredAt': Timestamp.fromDate(DateTime.utc(2026, 8, 2)),
          'inputMethod': 'VOICE',
          'createdAt': Timestamp.fromDate(DateTime.utc(2026, 8, 2)),
        },
      ),
    ];

    final activity = await repository.watchActivities('f1').first.then((items) => items.single);

    expect(activity.note, 'Damlama sulama yapıldı.');
    expect(activity.inputMethod, 'VOICE');
    expect(activity.timestamp.toUtc(), DateTime.utc(2026, 8, 2));
  });

  test('rejects a farm document with an invalid required field type', () async {
    final store = _RecordingStore();
    final repository = FirestoreFarmRepository(uid: 'uid-1', store: store);
    store.farms = [
      FirestoreFarmDocument(id: 'f1', data: {..._farmData(), 'latitude': '38.7'}),
    ];

    await expectLater(repository.watchFarms('uid-1').first, throwsA(isA<FormatException>()));
  });

  test('declares the named database and no client deletion operations', () {
    expect(FirestoreFarmContract.databaseId, 'tarla-asistani');
    expect(FirestoreFarmContract.farmFields, contains('ownerId'));
    expect(FirestoreFarmContract.activityFields, containsAll(['activityType', 'inputMethod']));
  });

  test('production adapter selects named database and executes owner query', () async {
    final firestore = FakeFirebaseFirestore();
    await firestore.collection('farms').doc('owned-farm').set(_farmData());
    await firestore.collection('farms').doc('other-farm').set({
      ..._farmData(),
      'ownerId': 'uid-2',
      'name': 'Başkasının tarlası',
    });
    final factory = _RecordingInstanceFactory(firestore);

    final store = FirebaseFirestoreFarmStore(instanceFactory: factory);
    final documents = await store.watchFarms('uid-1').first;

    expect(factory.databaseId, 'tarla-asistani');
    expect(documents, hasLength(1));
    expect(documents.single.id, 'owned-farm');
    expect(documents.single.data, _farmData());
  });

  test('production adapter orders and maps activity documents', () async {
    final firestore = FakeFirebaseFirestore();
    await firestore.doc('farms/f1/activities/older').set({
      ..._activityData(),
      'description': 'Önceki sulama',
      'occurredAt': Timestamp.fromDate(DateTime.utc(2026, 8, 1)),
    });
    await firestore.doc('farms/f1/activities/newer').set(_activityData());
    final store = FirebaseFirestoreFarmStore(
      instanceFactory: _RecordingInstanceFactory(firestore),
    );

    final documents = await store.watchActivities('f1').first;

    expect(documents.map((document) => document.id), ['newer', 'older']);
    expect(documents.first.data, _activityData());
  });

  test('production adapter writes encoded fields to the requested document', () async {
    final firestore = FakeFirebaseFirestore();
    final store = FirebaseFirestoreFarmStore(
      instanceFactory: _RecordingInstanceFactory(firestore),
    );
    final document = firestore.doc('farms/f1');
    final writeObserved = expectLater(
      document.snapshots(),
      emitsThrough(predicate<DocumentSnapshot<Map<String, dynamic>>>(
        (snapshot) => snapshot.exists,
      )),
    );

    await store.setDocument('farms/f1', {
      'ownerId': 'uid-1',
      'createdAt': const FirestoreServerTimestamp(),
    });
    await Future.any([
      writeObserved,
      store.writeFailures.first.then((failure) => throw failure.cause),
    ]);
    final snapshot = await document.get();

    expect(snapshot.data()!.keys, unorderedEquals(['ownerId', 'createdAt']));
    expect(snapshot.data()!['ownerId'], 'uid-1');
    expect(snapshot.data()!['createdAt'], isA<Timestamp>());
  });

  test('reports a later rejected locally accepted write', () async {
    final store = _RecordingStore();
    final repository = FirestoreFarmRepository(uid: 'uid-1', store: store);
    final failure = expectLater(
      repository.writeFailures,
      emits(isA<FirestoreWriteFailure>()),
    );

    await repository.createFarm(_farm());
    store.failLater('farms/f1', StateError('permission-denied'));

    await failure;
  });
}

Tarla _farm() => Tarla(id: 'f1', name: 'Deneme', latitude: 38.7, longitude: 35.4, size: 12.5, cropType: 'WHEAT', plantingDate: DateTime.utc(2026, 8, 1));
Faaliyet _activity({String inputMethod = 'MANUAL'}) => Faaliyet(id: 'a1', tarlaId: 'f1', type: 'OTHER', note: 'Damlama sulama yapıldı.', timestamp: DateTime.utc(2026, 8, 2), inputMethod: inputMethod);
Map<String, Object?> _farmData() => {
  'ownerId': 'uid-1', 'name': 'Deneme', 'latitude': 38.7, 'longitude': 35.4,
  'sizeInHectares': 12.5, 'cropType': 'WHEAT', 'irrigationMethod': 'OTHER',
  'plantedAt': Timestamp.fromDate(DateTime.utc(2026, 8, 1)),
  'createdAt': Timestamp.fromDate(DateTime.utc(2026, 8, 1)),
  'updatedAt': Timestamp.fromDate(DateTime.utc(2026, 8, 1)),
};
Map<String, Object?> _activityData() => {
  'activityType': 'OTHER',
  'description': 'Damlama sulama yapıldı.',
  'occurredAt': Timestamp.fromDate(DateTime.utc(2026, 8, 2)),
  'inputMethod': 'MANUAL',
  'createdAt': Timestamp.fromDate(DateTime.utc(2026, 8, 2)),
};

class _RecordingInstanceFactory implements FirebaseFirestoreInstanceFactory {
  _RecordingInstanceFactory(this.firestore);

  final FirebaseFirestore firestore;
  String? databaseId;

  @override
  FirebaseFirestore instanceFor({required String databaseId}) {
    this.databaseId = databaseId;
    return firestore;
  }
}

class _RecordingStore implements FirestoreFarmStore {
  String? path;
  Map<String, Object?> data = {};
  String? watchedOwnerId;
  List<FirestoreFarmDocument> farms = [];
  List<FirestoreActivityDocument> activities = [];
  final _failures = StreamController<FirestoreWriteFailure>.broadcast();
  @override Stream<FirestoreWriteFailure> get writeFailures => _failures.stream;
  void failLater(String path, Object error) => _failures.add(FirestoreWriteFailure(path: path, cause: error));
  @override Future<void> setDocument(String value, Map<String, Object?> valueData) async { path = value; data = Map.of(valueData); }
  @override Stream<List<FirestoreFarmDocument>> watchFarms(String ownerId) { watchedOwnerId = ownerId; return Stream.value(farms); }
  @override Stream<List<FirestoreActivityDocument>> watchActivities(String farmId) => Stream.value(activities);
}
