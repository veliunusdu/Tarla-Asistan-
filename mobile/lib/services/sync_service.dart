import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/foundation.dart';

import '../models/sync_operation.dart';
import 'api_client.dart';
import 'database_helper.dart';

enum SyncPhase { idle, offline, syncing, failed }

class SyncState {
  const SyncState({
    required this.phase,
    required this.pendingCount,
    this.message,
  });

  final SyncPhase phase;
  final int pendingCount;
  final String? message;
}

class SyncService {
  SyncService(
    this._apiClient, {
    SyncOperationStore? database,
    Connectivity? connectivity,
    Future<void> Function()? onConnectivityRestored,
  }) : _database = database ?? DatabaseHelper.instance,
       _connectivity = connectivity ?? Connectivity(),
       _onConnectivityRestored = onConnectivityRestored;

  final ApiClient _apiClient;
  final SyncOperationStore _database;
  final Connectivity _connectivity;
  final Future<void> Function()? _onConnectivityRestored;
  final state = ValueNotifier<SyncState>(
    const SyncState(phase: SyncPhase.idle, pendingCount: 0),
  );

  StreamSubscription<List<ConnectivityResult>>? _connectivitySubscription;
  bool _initialized = false;
  bool _running = false;

  Future<void> initialize() async {
    if (_initialized) {
      await syncNow();
      return;
    }
    _initialized = true;
    await refreshState();
    _connectivitySubscription = _connectivity.onConnectivityChanged.listen((
      results,
    ) {
      if (_hasConnection(results)) {
        syncNow();
        _syncAdditionalQueue();
      } else {
        _setOffline();
      }
    });
    final current = await _connectivity.checkConnectivity();
    if (_hasConnection(current)) {
      await syncNow();
      await _syncAdditionalQueue();
    } else {
      await _setOffline();
    }
  }

  Future<void> refreshState() async {
    final pending = await _database.getPendingSyncCount();
    state.value = SyncState(phase: SyncPhase.idle, pendingCount: pending);
  }

  Future<void> syncNow() async {
    if (_running) return;
    _running = true;
    try {
      var operations = await _database.getPendingSyncOperations();
      state.value = SyncState(
        phase: SyncPhase.syncing,
        pendingCount: operations.length,
        message: 'Kayıtlar eşitleniyor…',
      );
      for (final operation in operations) {
        try {
          await _apiClient.sendQueued(
            method: operation.method,
            endpoint: operation.endpoint,
            body: operation.payload,
          );
          await _database.markSyncCompleted(operation.id);
        } on ApiException catch (error) {
          await _database.markSyncFailed(operation.id, error.message);
          final pending = await _database.getPendingSyncCount();
          state.value = SyncState(
            phase: error.retryable ? SyncPhase.offline : SyncPhase.failed,
            pendingCount: pending,
            message: error.message,
          );
          return;
        }
      }
      operations = await _database.getPendingSyncOperations();
      state.value = SyncState(
        phase: SyncPhase.idle,
        pendingCount: operations.length,
        message: operations.isEmpty ? 'Tüm kayıtlar eşitlendi.' : null,
      );
    } finally {
      _running = false;
    }
  }

  Future<void> _setOffline() async {
    final pending = await _database.getPendingSyncCount();
    state.value = SyncState(
      phase: SyncPhase.offline,
      pendingCount: pending,
      message: pending > 0
          ? '$pending kayıt bağlantı gelince gönderilecek.'
          : 'Çevrimdışısınız. Yeni kayıtlar cihazda güvenle saklanacak.',
    );
  }

  Future<void> _syncAdditionalQueue() async {
    try {
      await _onConnectivityRestored?.call();
    } catch (error) {
      debugPrint('SyncService: additional queue sync failed: $error');
    }
  }

  bool _hasConnection(List<ConnectivityResult> results) =>
      results.any((result) => result != ConnectivityResult.none);

  Future<void> dispose() async {
    await _connectivitySubscription?.cancel();
    state.dispose();
  }
}
