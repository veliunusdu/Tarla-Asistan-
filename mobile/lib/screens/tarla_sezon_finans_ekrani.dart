import 'package:flutter/material.dart';
import '../features/finances/data/finance_repository.dart';
import '../features/finances/data/crop_period_source.dart';
import '../features/finances/data/financial_data_controller.dart';
import '../features/finances/domain/finance_models.dart';

class TarlaSezonFinansEkrani extends StatefulWidget {
  const TarlaSezonFinansEkrani({
    super.key,
    required this.farmId,
    required this.cropPeriodId,
    required this.farmName,
    required this.repository,
    this.periodSource,
  });
  final String farmId, cropPeriodId, farmName;
  final FinancialRepository repository;
  final FinanceCropPeriodSource? periodSource;
  @override
  State<TarlaSezonFinansEkrani> createState() => _TarlaSezonFinansEkraniState();
}

class _TarlaSezonFinansEkraniState extends State<TarlaSezonFinansEkrani> {
  late final FinancialDataController controller = FinancialDataController(
    widget.repository,
  );
  String selectedPeriodId = '';
  List<FinanceCropPeriod> periods = const [];
  @override
  void initState() {
    super.initState();
    selectedPeriodId = widget.cropPeriodId;
    controller.load(widget.farmId, widget.cropPeriodId);
    _loadPeriods();
  }

  Future<void> _loadPeriods() async {
    if (widget.periodSource == null) return;
    try {
      final loaded = await widget.periodSource!.listCropPeriods(widget.farmId);
      if (mounted) setState(() => periods = loaded);
    } catch (_) {}
  }

  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  String category(ExpenseCategory c) => switch (c) {
    ExpenseCategory.seed => 'Tohum',
    ExpenseCategory.fertilizer => 'Gübre',
    ExpenseCategory.pesticide => 'İlaç',
    ExpenseCategory.fuel => 'Mazot',
    ExpenseCategory.irrigation => 'Sulama',
    ExpenseCategory.labor => 'İşçilik',
    _ => 'Diğer',
  };
  Future<void> refresh() =>
      controller.refresh(widget.farmId, selectedPeriodId);

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) {
      final summary = controller.summary;
      final content = controller.isLoading && summary == null
          ? ListView(
              children: [
                SizedBox(height: 260),
                Center(child: CircularProgressIndicator()),
              ],
            )
          : controller.error != null && summary == null
          ? ListView(
              children: [
                const SizedBox(height: 180),
                const Center(child: Text('Finans verileri yüklenemedi.')),
                Center(
                  child: TextButton(
                    onPressed: refresh,
                    child: const Text('Tekrar Dene'),
                  ),
                ),
              ],
            )
          : summary == null
          ? ListView(
              children: [
                SizedBox(height: 180),
                Center(child: Text('Finans verisi bulunamadı.')),
              ],
            )
          : _content(context, summary);
      return Scaffold(
        appBar: AppBar(title: Text(widget.farmName)),
        body: RefreshIndicator(onRefresh: refresh, child: content),
      );
    },
  );

  Widget _content(BuildContext context, FinancialSummaryDto s) => ListView(
    padding: const EdgeInsets.all(16),
    children: [
      if (periods.length > 1)
        DropdownButton<String>(
          value: selectedPeriodId,
          isExpanded: true,
          items: periods
              .map((p) => DropdownMenuItem(value: p.id, child: Text(p.label)))
              .toList(),
          onChanged: (id) {
            if (id == null) return;
            setState(() => selectedPeriodId = id);
            controller.load(widget.farmId, id);
          },
        ),
      Text(
        '${s.cropName} • ${s.seasonYear}',
        style: Theme.of(context).textTheme.titleLarge,
      ),
      const SizedBox(height: 16),
      GridView.count(
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        crossAxisCount: 2,
        childAspectRatio: 1.8,
        children: [
          _metric('Toplam gider', money(s.totalExpense)),
          _metric('Kayıtlı gelir', money(s.totalRevenue)),
          _metric(
            'Kayıtlı gelir-gider farkı',
            signedMoney(s.registeredDifference),
          ),
          _metric(
            'Dekar başına gider',
            s.expensePerDecare == null
                ? 'Hesaplanamadı'
                : money(s.expensePerDecare!),
          ),
        ],
      ),
      if (s.warnings.isNotEmpty)
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: s.warnings.map(Text.new).toList(),
            ),
          ),
        ),
      _section('Giderler', 'Gider Ekle', () => _expenseForm(context)),
      if (controller.expenses.isEmpty)
        const Text('Henüz gider kaydı yok.')
      else
        ...controller.expenses.map(
          (e) => Card(
            child: ListTile(
              title: Text('${category(e.category)} • ${money(e.amount)}'),
              subtitle: Text(
                e.note ?? (e.isActivityLinked ? 'Faaliyetten' : ''),
              ),
              trailing: e.isActivityLinked
                  ? const Chip(label: Text('Faaliyetten'))
                  : PopupMenuButton<String>(
                      onSelected: (v) {
                        if (v == 'edit') _editExpense(e);
                        if (v == 'delete') _deleteExpense(e);
                      },
                      itemBuilder: (_) => const [
                        PopupMenuItem(value: 'edit', child: Text('Düzenle')),
                        PopupMenuItem(value: 'delete', child: Text('Sil')),
                      ],
                    ),
            ),
          ),
        ),
      _section('Satışlar', 'Satış Ekle', () => _saleForm(context)),
      if (controller.sales.isEmpty)
        const Text('Henüz satış kaydı yok.')
      else
        ...controller.sales.map(
          (x) => Card(
            child: ListTile(
              title: Text(
                '${x.harvestQuantity} ${x.unit} • ${money(x.totalAmount)}',
              ),
              subtitle: Text('${money(x.unitPrice)} / ${x.unit}'),
            trailing: PopupMenuButton<String>(
              onSelected: (v) { if (v == 'edit') _editSale(x); if (v == 'delete') _deleteSale(x); },
              itemBuilder: (_) => const [PopupMenuItem(value: 'edit', child: Text('Düzenle')), PopupMenuItem(value: 'delete', child: Text('Sil'))],
            ),
            ),
          ),
        ),
    ],
  );
  Widget _metric(String label, String value) => Card(
    child: Padding(
      padding: const EdgeInsets.all(10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label),
          const SizedBox(height: 5),
          Text(value, style: const TextStyle(fontWeight: FontWeight.bold)),
        ],
      ),
    ),
  );
  Widget _section(String title, String action, VoidCallback onPressed) => Row(
    mainAxisAlignment: MainAxisAlignment.spaceBetween,
    children: [
      Text(
        title,
        style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
      ),
      TextButton(onPressed: onPressed, child: Text(action)),
    ],
  );
  String money(double value) =>
      '${value.toStringAsFixed(2).replaceAll('.', ',')} ₺';
  String signedMoney(double value) => '${value >= 0 ? '+' : ''}${money(value)}';
  Future<void> _expenseForm(BuildContext context) async {
    final ok = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _ExpenseForm(
        repository: widget.repository,
        farmId: widget.farmId,
        periodId: selectedPeriodId,
      ),
    );
    if (ok == true) refresh();
  }

  Future<void> _editExpense(ExpenseDto e) async {
    final ok = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _ExpenseForm(
        repository: widget.repository,
        farmId: widget.farmId,
        periodId: selectedPeriodId,
        initial: e,
      ),
    );
    if (ok == true) refresh();
  }

  Future<void> _editSale(CropSaleDto s) async {
    final ok = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _SaleForm(
        repository: widget.repository,
        farmId: widget.farmId,
        periodId: selectedPeriodId,
        initial: s,
      ),
    );
    if (ok == true) refresh();
  }

  Future<void> _saleForm(BuildContext context) async {
    final ok = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _SaleForm(
        repository: widget.repository,
        farmId: widget.farmId,
        periodId: widget.cropPeriodId,
      ),
    );
    if (ok == true) refresh();
  }

  Future<void> _deleteExpense(ExpenseDto e) async {
    if (await _confirm('Bu gider kaydını silmek istiyor musunuz?')) {
      await widget.repository.archiveExpense(e.id);
      refresh();
    }
  }

  Future<void> _deleteSale(CropSaleDto s) async {
    if (await _confirm('Bu satış kaydını silmek istiyor musunuz?')) {
      await widget.repository.archiveSale(s.id);
      refresh();
    }
  }

  Future<bool> _confirm(String text) async =>
      await showDialog<bool>(
        context: context,
        builder: (_) => AlertDialog(
          title: Text(text),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Vazgeç'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Sil'),
            ),
          ],
        ),
      ) ??
      false;
}

class _ExpenseForm extends StatefulWidget {
  const _ExpenseForm({
    required this.repository,
    required this.farmId,
    required this.periodId,
    this.initial,
  });
  final FinancialRepository repository;
  final String farmId, periodId;
  final ExpenseDto? initial;
  @override
  State<_ExpenseForm> createState() => _ExpenseFormState();
}

class _ExpenseFormState extends State<_ExpenseForm> {
  ExpenseCategory? category;
  late final amount = TextEditingController(
    text: widget.initial?.amount.toString(),
  );
  late final note = TextEditingController(text: widget.initial?.note ?? '');
  bool saving = false;
  @override
  void initState() {
    super.initState();
    category = widget.initial?.category;
  }

  @override
  void dispose() {
    amount.dispose();
    note.dispose();
    super.dispose();
  }

  Future<void> save() async {
    final value = double.tryParse(amount.text.replaceAll(',', '.'));
    if (category == null || value == null || value <= 0) {
      setState(() {});
      return;
    }
    setState(() => saving = true);
    try {
      if (widget.initial != null) {
        await widget.repository.updateExpense(
          widget.initial!.id,
          category: category!,
          amount: value,
          occurredAtUtc: widget.initial!.occurredAtUtc,
          note: note.text.trim().isEmpty ? null : note.text.trim(),
        );
      } else
        await widget.repository.createExpense(
          widget.farmId,
          widget.periodId,
          category: category!,
          amount: value,
          occurredAtUtc: DateTime.now().toUtc(),
          note: note.text.trim().isEmpty ? null : note.text.trim(),
        );
      if (mounted) Navigator.pop(context, true);
    } finally {
      if (mounted) setState(() => saving = false);
    }
  }

  @override
  Widget build(BuildContext c) => Padding(
    padding: EdgeInsets.fromLTRB(
      16,
      16,
      16,
      MediaQuery.viewInsetsOf(c).bottom + 16,
    ),
    child: Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        DropdownButtonFormField<ExpenseCategory>(
          decoration: const InputDecoration(labelText: 'Kategori'),
          items: ExpenseCategory.values
              .where((e) => e != ExpenseCategory.unknown)
              .map((e) => DropdownMenuItem(value: e, child: Text(e.name)))
              .toList(),
          onChanged: (v) => setState(() => category = v),
        ),
        TextField(
          controller: amount,
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          decoration: const InputDecoration(labelText: 'Tutar (TL)'),
        ),
        TextField(
          controller: note,
          maxLength: 500,
          decoration: const InputDecoration(labelText: 'Not'),
        ),
        FilledButton(
          onPressed: saving ? null : save,
          child: Text(saving ? 'Kaydediliyor...' : 'Kaydet'),
        ),
      ],
    ),
  );
}

class _SaleForm extends StatefulWidget {
  const _SaleForm({
    required this.repository,
    required this.farmId,
    required this.periodId,
    this.initial,
  });
  final FinancialRepository repository;
  final String farmId, periodId;
  final CropSaleDto? initial;
  @override
  State<_SaleForm> createState() => _SaleFormState();
}

class _SaleFormState extends State<_SaleForm> {
  late final quantity = TextEditingController(
    text: widget.initial?.harvestQuantity.toString(),
  );
  late final price = TextEditingController(
    text: widget.initial?.unitPrice.toString(),
  );
  late final note = TextEditingController(
    text: widget.initial?.buyerOrMarketNote ?? '',
  );
  late String unit = widget.initial?.unit ?? 'kg';
  bool saving = false;
  @override
  void dispose() {
    quantity.dispose();
    price.dispose();
    note.dispose();
    super.dispose();
  }

  Future<void> save() async {
    final q = double.tryParse(quantity.text.replaceAll(',', '.'));
    final p = double.tryParse(price.text.replaceAll(',', '.'));
    if (q == null || q <= 0 || p == null || p <= 0) {
      setState(() {});
      return;
    }
    setState(() => saving = true);
    try {
      if (widget.initial != null) {
        await widget.repository.updateSale(
          widget.initial!.id,
          harvestQuantity: q,
          unit: unit,
          unitPrice: p,
          soldAt: widget.initial!.soldAt,
          buyerOrMarketNote: note.text.trim().isEmpty ? null : note.text.trim(),
        );
      } else
        await widget.repository.createSale(
          widget.farmId,
          widget.periodId,
          harvestQuantity: q,
          unit: unit,
          unitPrice: p,
          soldAt: DateOnly(
            DateTime.now().year,
            DateTime.now().month,
            DateTime.now().day,
          ),
          buyerOrMarketNote: note.text.trim().isEmpty ? null : note.text.trim(),
        );
      if (mounted) Navigator.pop(context, true);
    } finally {
      if (mounted) setState(() => saving = false);
    }
  }

  @override
  Widget build(BuildContext c) => Padding(
    padding: EdgeInsets.fromLTRB(
      16,
      16,
      16,
      MediaQuery.viewInsetsOf(c).bottom + 16,
    ),
    child: Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        TextField(
          controller: quantity,
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          decoration: const InputDecoration(labelText: 'Miktar'),
        ),
        DropdownButtonFormField<String>(
          initialValue: unit,
          items: const [
            DropdownMenuItem(value: 'kg', child: Text('kg')),
            DropdownMenuItem(value: 'ton', child: Text('ton')),
          ],
          onChanged: (v) => setState(() => unit = v ?? 'kg'),
        ),
        TextField(
          controller: price,
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          decoration: const InputDecoration(labelText: 'Birim fiyat (TL)'),
        ),
        TextField(
          controller: note,
          maxLength: 500,
          decoration: const InputDecoration(labelText: 'Alıcı veya pazar notu'),
        ),
        FilledButton(
          onPressed: saving ? null : save,
          child: Text(saving ? 'Kaydediliyor...' : 'Kaydet'),
        ),
      ],
    ),
  );
}
