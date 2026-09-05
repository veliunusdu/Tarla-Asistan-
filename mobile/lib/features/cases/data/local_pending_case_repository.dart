import 'dart:io';
import 'dart:typed_data';

import 'package:path/path.dart' as p;
import 'package:sqflite/sqflite.dart';
import 'package:uuid/uuid.dart';

import '../../../services/database_helper.dart';
import '../domain/models/pending_case_submission.dart';

class LocalPendingCaseRepository {
  const LocalPendingCaseRepository({
    this.databaseProvider,
    this.userIdProvider,
    this.uuid = const Uuid(),
  });

  final Future<Database> Function()? databaseProvider;
  final String? Function()? userIdProvider;
  final Uuid uuid;

  Future<Database> get _database =>
      databaseProvider?.call() ?? DatabaseHelper.instance.database;
  String? get _userId =>
      userIdProvider?.call() ?? DatabaseHelper.instance.currentUserId;

  Future<String> enqueue({
    required String farmId,
    required String category,
    required String title,
    required String description,
    required String clientOperationId,
    Uint8List? imageBytes,
    String? uploadedMediaId,
    String? audioFilePath,
    String? uploadedAudioMediaId,
  }) async {
    final userId = _userId;
    if (userId == null || userId.isEmpty)
      throw StateError('Oturum açmış kullanıcı bulunamadı.');
    final db = await _database;
    final id = uuid.v4();
    String? imagePath;
    try {
      if (imageBytes != null && imageBytes.isNotEmpty) {
        final dir = Directory(
          p.join(p.dirname(await getDatabasesPath()), 'pending_cases', userId),
        );
        await dir.create(recursive: true);
        imagePath = p.join(dir.path, '$id.jpg');
        await File(imagePath).writeAsBytes(imageBytes, flush: true);
      }
      final pending = PendingCaseSubmission(
        id: id,
        userId: userId,
        farmId: farmId,
        category: category,
        title: title.trim(),
        description: description.trim(),
        clientOperationId: clientOperationId,
        localImagePath: imagePath,
        uploadedMediaId: uploadedMediaId,
        localAudioPath: audioFilePath,
        uploadedAudioMediaId: uploadedAudioMediaId,
        createdAtUtc: DateTime.now().toUtc(),
      );
      await db.insert(
        'pending_case_submissions',
        await _compatibleMap(db, pending.toMap()),
        conflictAlgorithm: ConflictAlgorithm.abort,
      );
      return id;
    } catch (_) {
      if (imagePath != null) await _deleteFile(imagePath);
      rethrow;
    }
  }

  Future<List<PendingCaseSubmission>> getPending({String? userId}) async {
    final active = userId ?? _userId;
    if (active == null || active.isEmpty) return const [];
    final rows = await (await _database).query(
      'pending_case_submissions',
      where: 'user_id = ? AND state = ?',
      whereArgs: [active, PendingCaseState.pending.name],
      orderBy: 'created_at_utc ASC',
    );
    return rows.map(PendingCaseSubmission.fromMap).toList();
  }

  Future<void> update(PendingCaseSubmission submission) async {
    final db = await _database;
    await db.update(
      'pending_case_submissions',
      await _compatibleMap(db, submission.toMap()),
      where: 'id = ? AND user_id = ?',
      whereArgs: [submission.id, submission.userId],
    );
  }

  Future<Map<String, dynamic>> _compatibleMap(
    Database db,
    Map<String, dynamic> map,
  ) async {
    final columns = (await db.rawQuery(
      'PRAGMA table_info(pending_case_submissions)',
    )).map((row) => row['name'] as String).toSet();
    return Map<String, dynamic>.fromEntries(
      map.entries.where((entry) => columns.contains(entry.key)),
    );
  }

  Future<void> remove(PendingCaseSubmission submission) async {
    await (await _database).delete(
      'pending_case_submissions',
      where: 'id = ? AND user_id = ?',
      whereArgs: [submission.id, submission.userId],
    );
    if (submission.localImagePath != null)
      await _deleteFile(submission.localImagePath!);
    if (submission.localAudioPath != null)
      await _deleteFile(submission.localAudioPath!);
  }

  Future<void> clearUser(String userId) async {
    final db = await _database;
    final rows = await db.query(
      'pending_case_submissions',
      where: 'user_id = ?',
      whereArgs: [userId],
    );
    await db.delete(
      'pending_case_submissions',
      where: 'user_id = ?',
      whereArgs: [userId],
    );
    for (final row in rows) {
      final path = row['local_image_path'] as String?;
      if (path != null) await _deleteFile(path);
      final audioPath = row['local_audio_path'] as String?;
      if (audioPath != null) await _deleteFile(audioPath);
    }
  }

  Future<void> _deleteFile(String path) async {
    try {
      await File(path).delete();
    } on FileSystemException {
      // Cleanup failure must not recreate or resend a successful case.
    }
  }
}
