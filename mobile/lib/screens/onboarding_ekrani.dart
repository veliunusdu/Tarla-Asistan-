import 'package:flutter/material.dart';

class OnboardingEkrani extends StatefulWidget {
  const OnboardingEkrani({super.key, required this.onFinished});

  final Future<void> Function() onFinished;

  @override
  State<OnboardingEkrani> createState() => _OnboardingEkraniState();
}

class _OnboardingEkraniState extends State<OnboardingEkrani> {
  final _controller = PageController();
  int _currentPage = 0;
  bool _finishing = false;

  static const _pages = [
    (
      title: "Tarla Asistanı'na hoş geldiniz",
      description: 'Tarlalarınızı ve günlük işlerinizi tek yerden takip edin.',
      icon: Icons.grass,
    ),
    (
      title: 'Sahada çevrimdışı çalışın',
      description:
          'İşlerinizi bağlantı olmasa da kaydedin; internet gelince otomatik gönderilsin.',
      icon: Icons.cloud_off_outlined,
    ),
    (
      title: 'Kritik uyarıları kaçırmayın',
      description:
          'Planlanan işler, hava riski ve uzman cevaplarına gelen bildirimlerden doğrudan ulaşın.',
      icon: Icons.notifications_active_outlined,
    ),
  ];

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _next() async {
    if (_currentPage < _pages.length - 1) {
      await _controller.nextPage(
        duration: const Duration(milliseconds: 250),
        curve: Curves.easeInOut,
      );
      return;
    }
    setState(() => _finishing = true);
    await widget.onFinished();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            Expanded(
              child: PageView.builder(
                controller: _controller,
                itemCount: _pages.length,
                onPageChanged: (index) => setState(() => _currentPage = index),
                itemBuilder: (context, index) {
                  final page = _pages[index];
                  return SingleChildScrollView(
                    padding: const EdgeInsets.all(32),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(page.icon, size: 88, semanticLabel: page.title),
                        const SizedBox(height: 32),
                        Semantics(
                          header: true,
                          child: Text(
                            page.title,
                            textAlign: TextAlign.center,
                            style: Theme.of(context).textTheme.headlineMedium,
                          ),
                        ),
                        const SizedBox(height: 16),
                        Text(
                          page.description,
                          textAlign: TextAlign.center,
                          style: Theme.of(context).textTheme.bodyLarge,
                        ),
                      ],
                    ),
                  );
                },
              ),
            ),
            Semantics(
              label: '${_currentPage + 1} / ${_pages.length}. sayfa',
              child: Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: List.generate(
                  _pages.length,
                  (index) => Container(
                    width: index == _currentPage ? 24 : 8,
                    height: 8,
                    margin: const EdgeInsets.all(4),
                    decoration: BoxDecoration(
                      color: index == _currentPage
                          ? Theme.of(context).colorScheme.primary
                          : Theme.of(context).colorScheme.outlineVariant,
                      borderRadius: BorderRadius.circular(8),
                    ),
                  ),
                ),
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(24),
              child: SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: _finishing ? null : _next,
                  child: Text(
                    _currentPage == _pages.length - 1 ? 'Girişe geç' : 'İlerle',
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
