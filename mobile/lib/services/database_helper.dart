import 'package:sqflite/sqflite.dart';
import 'package:path/path.dart';
import '../models/tarla.dart'; 
import '../models/faaliyet.dart';

class DatabaseHelper {
  static final DatabaseHelper instance = DatabaseHelper._init();
  static Database? _database;

  DatabaseHelper._init();

  Future<Database> get database async {
    if (_database != null) return _database!;
    _database = await _initDB('tarla_asistani.db');
    return _database!;
  }

  Future<Database> _initDB(String filePath) async {
    final dbPath = await getDatabasesPath();
    final path = join(dbPath, filePath);

    return await openDatabase(
      path,
      version: 2, // Versiyonu 2'ye çıkardık
      onCreate: _createDB,
    );
  }

  Future _createDB(Database db, int version) async {
    // Tarla Tablosu
    await db.execute('''
      CREATE TABLE tarlalar (
        id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        latitude REAL NOT NULL,
        longitude REAL NOT NULL,
        size REAL NOT NULL,
        cropType TEXT NOT NULL,
        plantingDate TEXT NOT NULL
      )
    ''');
    
    // Faaliyet Tablosu (dueDate ve isCompleted eklendi)
    await db.execute('''
      CREATE TABLE faaliyetler (
        id TEXT PRIMARY KEY,
        tarlaId TEXT NOT NULL,
        type TEXT NOT NULL,
        note TEXT,
        audioPath TEXT,
        photos TEXT,
        timestamp TEXT NOT NULL,
        dueDate TEXT,
        isCompleted INTEGER
      )
    ''');
  }

  // --- TARLA METODLARI ---

  Future<int> insertTarla(Tarla tarla) async {
    final db = await instance.database;
    return await db.insert(
      'tarlalar',
      tarla.toJson(),
      conflictAlgorithm: ConflictAlgorithm.replace,
    );
  }

  Future<List<Tarla>> getTarlalar() async {
    final db = await instance.database;
    final List<Map<String, dynamic>> result = await db.query('tarlalar');
    
    return result.map((json) => Tarla.fromJson(json)).toList();
  }

  // --- FAALİYET METODLARI ---

  // Yeni faaliyet ekleme
  Future<int> insertFaaliyet(Faaliyet faaliyet) async {
    final db = await instance.database;
    return await db.insert('faaliyetler', faaliyet.toJson());
  }

  // Belirli bir tarlaya ait faaliyetleri getirme
  Future<List<Faaliyet>> getFaaliyetler(String tarlaId) async {
    final db = await instance.database;
    final List<Map<String, dynamic>> result = await db.query(
      'faaliyetler', 
      where: 'tarlaId = ?', 
      whereArgs: [tarlaId],
      orderBy: 'timestamp DESC',
    );
    
    return result.map((json) => Faaliyet.fromJson(json)).toList();
  }

  // --- İSTATİSTİK SORGULARI ---

  Future<int> getTarlaSayisi() async {
    final db = await instance.database;
    final result = await db.rawQuery('SELECT COUNT(*) FROM tarlalar');
    return Sqflite.firstIntValue(result) ?? 0;
  }

  Future<double> getToplamDonum() async {
    final db = await instance.database;
    final result = await db.rawQuery('SELECT SUM(size) as total FROM tarlalar');
    return (result.first['total'] as num?)?.toDouble() ?? 0.0;
  }

  // --- SİLME İŞLEMLERİ ---

  Future<int> deleteFaaliyet(String id) async {
    final db = await instance.database;
    return await db.delete(
      'faaliyetler',
      where: 'id = ?',
      whereArgs: [id],
    );
  }
}