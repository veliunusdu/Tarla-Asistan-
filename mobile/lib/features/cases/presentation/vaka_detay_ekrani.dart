import 'package:flutter/material.dart';

import '../data/case_repository.dart';

/// Placeholder screen for Task 4 interactive chat implementation.
class VakaDetayEkrani extends StatelessWidget {
  const VakaDetayEkrani({
    super.key,
    required this.caseId,
    required this.caseRepository,
  });

  final String caseId;
  final CaseRepository caseRepository;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Vaka Detayı'),
      ),
      body: Center(
        child: Text('Vaka Detayı: $caseId'),
      ),
    );
  }
}
