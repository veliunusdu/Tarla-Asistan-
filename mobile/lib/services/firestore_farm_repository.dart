import 'dart:async';

import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_core/firebase_core.dart';

import '../models/faaliyet.dart';
import '../models/tarla.dart';

/// The Firestore boundary used by the repository. Tests use an in-memory
/// implementation so they never make remote Firestore writes.
abstract class FirestoreFarmStore {
  Stream<List<FirestoreFarmDocument>> watchFarms(String ownerId);

  Stream<List<FirestoreActivityDocument>> watchActivities(String farmId);

  Future<void> setDocument(String path, Map<String, Object?> data);

  Stream<FirestoreWriteFailure> get writeFailures;
}

class FirestoreWriteFailure {
  const FirestoreWriteFailure({required this.path, required this.cause});

  final String path;
  final Object cause;
}

abstract final class FirestoreFarmContract {
  static const databaseId = 'tarla-asistani';
  static const farmFields = {
    'ownerId',
    'name',
    'latitude',
    'longitude',
    'sizeInHectares',
    'cropType',
    'irrigationMethod',
    'plantedAt',
    'createdAt',
    'updatedAt',
  };
  static const activityFields = {
    'activityType',
    'description',
    'occurredAt',
    'inputMethod',
    'createdAt',
  };
}

class FirestoreFarmDocument {
  const FirestoreFarmDocument({required this.id, required this.data});

  final String id;
  final Map<String, Object?> data;
}

class FirestoreActivityDocument {
  const FirestoreActivityDocument({required this.id, required this.data});

  final String id;
  final Map<String, Object?> data;
}

class FirestoreFarmRepository {
  FirestoreFarmRepository({required this.uid, FirestoreFarmStore? store})
    : _store = store ?? FirebaseFirestoreFarmStore();

  final String uid;
  final FirestoreFarmStore _store;

  Stream<FirestoreWriteFailure> get writeFailures => _store.writeFailures;

  Stream<List<Tarla>> watchFarms(String uid) => _store
      .watchFarms(uid)
      .map((documents) => documents.map(_farmFromDocument).toList());

  Stream<List<Faaliyet>> watchActivities(String farmId) => _store
      .watchActivities(farmId)
      .map(
        (documents) => documents.map((document) {
          final data = document.data;
          return Faaliyet(
            id: document.id,
            tarlaId: farmId,
            type: _requiredString(data, 'activityType'),
            note: _requiredString(data, 'description'),
            timestamp: _requiredTimestamp(data, 'occurredAt'),
            inputMethod: _requiredInputMethod(data),
            isCompleted: true,
          );
        }).toList(),
      );

  Future<void> createFarm(Tarla farm) =>
      _store.setDocument('farms/${farm.id}', {
        'ownerId': uid,
        'name': farm.name,
        'latitude': farm.latitude,
        'longitude': farm.longitude,
        'sizeInHectares': farm.size,
        'cropType': farm.cropType,
        'irrigationMethod': 'OTHER',
        'plantedAt': farm.plantingDate,
        'createdAt': const FirestoreServerTimestamp(),
        'updatedAt': const FirestoreServerTimestamp(),
      });

  Future<void> createActivity(String farmId, Faaliyet activity) =>
      _store.setDocument('farms/$farmId/activities/${activity.id}', {
        'activityType': 'OTHER',
        'description': activity.note.trim().isEmpty
            ? activity.type
            : activity.note.trim(),
        'occurredAt': activity.timestamp,
        'inputMethod': activity.inputMethod,
        'createdAt': const FirestoreServerTimestamp(),
      });

  Tarla _farmFromDocument(FirestoreFarmDocument document) {
    final data = document.data;
    return Tarla(
      id: document.id,
      name: _requiredString(data, 'name'),
      latitude: _requiredNumber(data, 'latitude'),
      longitude: _requiredNumber(data, 'longitude'),
      size: _requiredNumber(data, 'sizeInHectares'),
      cropType: _requiredString(data, 'cropType'),
      plantingDate: _requiredTimestamp(data, 'plantedAt'),
    );
  }

  static String _requiredString(Map<String, Object?> data, String key) {
    final value = data[key];
    if (value is String && value.isNotEmpty) return value;
    throw FormatException('Geçersiz Firestore tarla verisi: $key');
  }

  static double _requiredNumber(Map<String, Object?> data, String key) {
    final value = data[key];
    if (value is num) return value.toDouble();
    throw FormatException('Geçersiz Firestore tarla verisi: $key');
  }

  static DateTime _requiredTimestamp(Map<String, Object?> data, String key) {
    final value = data[key];
    if (value is Timestamp) return value.toDate();
    throw FormatException('Geçersiz Firestore tarla verisi: $key');
  }

  static String _requiredInputMethod(Map<String, Object?> data) {
    final method = _requiredString(data, 'inputMethod');
    if (method == 'MANUAL' || method == 'VOICE') return method;
    throw const FormatException(
      'Geçersiz Firestore faaliyet verisi: inputMethod',
    );
  }
}

class FirestoreServerTimestamp {
  const FirestoreServerTimestamp();
}

abstract class FirebaseFirestoreInstanceFactory {
  FirebaseFirestore instanceFor({required String databaseId});
}

class FirebaseFirestoreSdkInstanceFactory
    implements FirebaseFirestoreInstanceFactory {
  const FirebaseFirestoreSdkInstanceFactory();

  @override
  FirebaseFirestore instanceFor({required String databaseId}) =>
      FirebaseFirestore.instanceFor(
        app: Firebase.app(),
        databaseId: databaseId,
      );
}

abstract final class FirestoreMapEncoder {
  static Map<String, dynamic> encode(Map<String, Object?> data) => data.map(
    (key, value) => MapEntry(
      key,
      value is FirestoreServerTimestamp ? FieldValue.serverTimestamp() : value,
    ),
  );
}

class FirebaseFirestoreFarmStore implements FirestoreFarmStore {
  FirebaseFirestoreFarmStore({
    FirebaseFirestore? firestore,
    FirebaseFirestoreInstanceFactory? instanceFactory,
  }) : _firestore =
           firestore ??
           (instanceFactory ?? const FirebaseFirestoreSdkInstanceFactory())
               .instanceFor(databaseId: FirestoreFarmContract.databaseId);

  final FirebaseFirestore _firestore;
  final _writeFailures = StreamController<FirestoreWriteFailure>.broadcast();

  @override
  Stream<FirestoreWriteFailure> get writeFailures => _writeFailures.stream;

  @override
  Stream<List<FirestoreFarmDocument>> watchFarms(String ownerId) => _firestore
      .collection('farms')
      .where('ownerId', isEqualTo: ownerId)
      .snapshots()
      .map(
        (snapshot) => snapshot.docs
            .map(
              (document) => FirestoreFarmDocument(
                id: document.id,
                data: Map<String, Object?>.from(document.data()),
              ),
            )
            .toList(),
      );

  @override
  Stream<List<FirestoreActivityDocument>> watchActivities(String farmId) =>
      _firestore
          .collection('farms')
          .doc(farmId)
          .collection('activities')
          .orderBy('occurredAt', descending: true)
          .snapshots()
          .map(
            (snapshot) => snapshot.docs
                .map(
                  (document) => FirestoreActivityDocument(
                    id: document.id,
                    data: Map<String, Object?>.from(document.data()),
                  ),
                )
                .toList(),
          );

  @override
  Future<void> setDocument(String path, Map<String, Object?> data) {
    // Firestore queues writes locally. Do not hold the form open for a server
    // acknowledgement; a later remote failure is retained by the SDK retry path.
    unawaited(
      _firestore.doc(path).set(FirestoreMapEncoder.encode(data)).catchError((
        error,
      ) {
        _writeFailures.add(FirestoreWriteFailure(path: path, cause: error));
      }),
    );
    return Future.value();
  }
}
