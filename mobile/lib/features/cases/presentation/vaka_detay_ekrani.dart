import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import '../../../app/theme/app_colors.dart';
import '../../../app/theme/app_spacing.dart';
import '../../../services/api_client.dart';
import '../../ai_assistant/data/image_picker_service.dart';
import '../data/case_repository.dart';
import '../domain/models/case_category.dart';
import '../domain/models/case_detail.dart';
import '../domain/models/case_message.dart';
import '../domain/models/case_status.dart';

class VakaDetayEkrani extends StatefulWidget {
  const VakaDetayEkrani({
    super.key,
    required this.caseId,
    required this.caseRepository,
    this.imagePickerService,
  });

  final String caseId;
  final CaseRepository caseRepository;
  final ImagePickerService? imagePickerService;

  @override
  State<VakaDetayEkrani> createState() => _VakaDetayEkraniState();
}

class _VakaDetayEkraniState extends State<VakaDetayEkrani> {
  final TextEditingController _textController = TextEditingController();
  final ScrollController _scrollController = ScrollController();
  late final ImagePickerService _pickerService;

  CaseDetail? _detail;
  bool _isLoading = true;
  String? _errorMessage;
  bool _isSending = false;
  PickedImageData? _selectedImage;

  @override
  void initState() {
    super.initState();
    _pickerService = widget.imagePickerService ?? const DefaultImagePickerService();
    _loadCase();
  }

  @override
  void dispose() {
    _textController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  Future<void> _loadCase() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      final detail = await widget.caseRepository.getCaseById(widget.caseId);
      if (!mounted) return;
      setState(() {
        _detail = detail;
        _isLoading = false;
      });
      _scrollToBottom();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _errorMessage = e.toString();
        _isLoading = false;
      });
    }
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollController.hasClients) {
        _scrollController.animateTo(
          _scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 300),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Color _getStatusColor(CaseStatus status) => switch (status) {
        CaseStatus.waitingFarmer => AppColors.error,
        CaseStatus.inReview => AppColors.warning,
        CaseStatus.open => AppColors.primary,
        CaseStatus.answered => AppColors.success,
        CaseStatus.closed => Colors.grey,
      };

  IconData _getCategoryIcon(CaseCategory category) => switch (category) {
        CaseCategory.disease => Icons.coronavirus_outlined,
        CaseCategory.pest => Icons.pest_control_outlined,
        CaseCategory.irrigation => Icons.water_drop_outlined,
        CaseCategory.nutrition => Icons.eco_outlined,
        CaseCategory.weather => Icons.cloud_outlined,
        CaseCategory.other => Icons.help_outline,
      };

  String _formatDateTime(DateTime date) {
    final day = date.day.toString().padLeft(2, '0');
    final month = date.month.toString().padLeft(2, '0');
    final year = date.year.toString();
    final hour = date.hour.toString().padLeft(2, '0');
    final minute = date.minute.toString().padLeft(2, '0');
    return '$day.$month.$year $hour:$minute';
  }

  Future<void> _pickPhoto() async {
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            ListTile(
              leading: const Icon(Icons.camera_alt),
              title: const Text('Kameradan Çek'),
              onTap: () => Navigator.pop(ctx, ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library),
              title: const Text('Galeriden Seç'),
              onTap: () => Navigator.pop(ctx, ImageSource.gallery),
            ),
          ],
        ),
      ),
    );

    if (source == null || !mounted) return;
    final picked = await _pickerService.pickImage(source: source);
    if (picked != null && mounted) {
      setState(() => _selectedImage = picked);
    }
  }

  Future<void> _sendMessage() async {
    final text = _textController.text.trim();
    if (text.isEmpty && _selectedImage == null) return;
    if (_isSending) return;

    setState(() => _isSending = true);

    final imageBytes = _selectedImage?.bytes;
    final imageName = _selectedImage?.name;

    try {
      final newMsg = await widget.caseRepository.sendMessage(
        widget.caseId,
        body: text,
        imageBytes: imageBytes,
        imageFileName: imageName,
      );

      if (!mounted) return;

      setState(() {
        _textController.clear();
        _selectedImage = null;
        if (_detail != null) {
          final updatedMessages = [..._detail!.messages, newMsg];
          final updatedStatus = _detail!.status == CaseStatus.waitingFarmer
              ? CaseStatus.inReview
              : _detail!.status;
          _detail = CaseDetail(
            id: _detail!.id,
            farmId: _detail!.farmId,
            farmName: _detail!.farmName,
            category: _detail!.category,
            status: updatedStatus,
            title: _detail!.title,
            description: _detail!.description,
            initialMediaUrls: _detail!.initialMediaUrls,
            messages: updatedMessages,
            createdAt: _detail!.createdAt,
          );
        }
      });

      _scrollToBottom();
    } catch (e) {
      if (!mounted) return;
      final errorMsg = e is ApiException
          ? e.message
          : 'Mesaj iletilemedi. Lütfen bağlantınızı kontrol edip tekrar deneyin.';
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(
          SnackBar(
            backgroundColor: AppColors.error,
            content: Text(errorMsg),
          ),
        );
    } finally {
      if (mounted) setState(() => _isSending = false);
    }
  }

  Widget _buildMediaThumbnail(String url) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(8),
      child: Image.network(
        url,
        width: 72,
        height: 72,
        fit: BoxFit.cover,
        errorBuilder: (context, error, stackTrace) => Container(
          width: 72,
          height: 72,
          color: Colors.grey.shade200,
          child: const Icon(Icons.image_not_supported, color: Colors.grey, size: 24),
        ),
        loadingBuilder: (context, child, progress) {
          if (progress == null) return child;
          return Container(
            width: 72,
            height: 72,
            color: Colors.grey.shade100,
            child: const Center(
              child: SizedBox(
                width: 20,
                height: 20,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            ),
          );
        },
      ),
    );
  }

  Widget _buildHeaderCard(CaseDetail detail) {
    final statusColor = _getStatusColor(detail.status);

    return Card(
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: Colors.grey.shade300),
      ),
      color: Colors.grey.shade50,
      child: Padding(
        padding: const EdgeInsets.all(AppSpacing.md),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                // Category badge
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: 4,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.primary.withAlpha(20),
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(
                        _getCategoryIcon(detail.category),
                        size: 14,
                        color: AppColors.primary,
                      ),
                      const SizedBox(width: 4),
                      Text(
                        detail.category.displayName,
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: AppColors.primary,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                // Farm name chip
                Flexible(
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: Colors.grey.shade200,
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Text(
                      detail.farmName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                // Status badge
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: 4,
                  ),
                  decoration: BoxDecoration(
                    color: statusColor.withAlpha(25),
                    borderRadius: BorderRadius.circular(6),
                    border: Border.all(color: statusColor.withAlpha(80)),
                  ),
                  child: Text(
                    detail.status.displayName,
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.bold,
                      color: statusColor,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.sm),
            Text(
              detail.title,
              style: const TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.bold,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              detail.description,
              style: const TextStyle(
                fontSize: 14,
                color: AppColors.textSecondary,
              ),
            ),
            if (detail.initialMediaUrls.isNotEmpty) ...[
              const SizedBox(height: AppSpacing.sm),
              SizedBox(
                height: 72,
                child: ListView.separated(
                  scrollDirection: Axis.horizontal,
                  itemCount: detail.initialMediaUrls.length,
                  separatorBuilder: (context, index) => const SizedBox(width: 8),
                  itemBuilder: (context, index) {
                    return _buildMediaThumbnail(detail.initialMediaUrls[index]);
                  },
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildMessageBubble(CaseMessage msg) {
    if (msg.isFromExpert) {
      return Align(
        alignment: Alignment.centerLeft,
        child: ConstrainedBox(
          constraints: BoxConstraints(
            maxWidth: MediaQuery.of(context).size.width * 0.82,
          ),
          child: Container(
            margin: const EdgeInsets.only(bottom: AppSpacing.sm),
            padding: const EdgeInsets.all(AppSpacing.sm),
            decoration: BoxDecoration(
              color: const Color(0xFFE8F5E9),
              borderRadius: const BorderRadius.only(
                topLeft: Radius.circular(12),
                topRight: Radius.circular(12),
                bottomLeft: Radius.circular(2),
                bottomRight: Radius.circular(12),
              ),
              border: Border.all(color: AppColors.primary.withAlpha(40)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.verified_user_outlined, size: 15, color: AppColors.primary),
                    const SizedBox(width: 4),
                    const Text(
                      'Ziraat Mühendisi',
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.bold,
                        color: AppColors.primary,
                      ),
                    ),
                    if (msg.senderName.isNotEmpty && msg.senderName != 'Ziraat Mühendisi') ...[
                      const SizedBox(width: 4),
                      Flexible(
                        child: Text(
                          '(${msg.senderName})',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontSize: 11,
                            color: Colors.grey.shade700,
                          ),
                        ),
                      ),
                    ],
                  ],
                ),
                if (msg.messageType == CaseMessageType.additionalInfoRequest) ...[
                  const SizedBox(height: 4),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                    decoration: BoxDecoration(
                      color: AppColors.warning.withAlpha(30),
                      borderRadius: BorderRadius.circular(4),
                    ),
                    child: const Text(
                      '⚠️ Ek Bilgi Talebi',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        color: AppColors.warning,
                      ),
                    ),
                  ),
                ] else if (msg.messageType == CaseMessageType.expertResponse) ...[
                  const SizedBox(height: 4),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                    decoration: BoxDecoration(
                      color: AppColors.success.withAlpha(30),
                      borderRadius: BorderRadius.circular(4),
                    ),
                    child: const Text(
                      '✅ Ziraat Mühendisi Tavsiyesi',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        color: AppColors.success,
                      ),
                    ),
                  ),
                ],
                const SizedBox(height: 6),
                Text(
                  msg.body,
                  style: const TextStyle(
                    fontSize: 14,
                    color: AppColors.textPrimary,
                  ),
                ),
                if (msg.mediaUrls.isNotEmpty) ...[
                  const SizedBox(height: 6),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: msg.mediaUrls.map((url) => _buildMediaThumbnail(url)).toList(),
                  ),
                ],
                const SizedBox(height: 4),
                Align(
                  alignment: Alignment.bottomRight,
                  child: Text(
                    _formatDateTime(msg.createdAt),
                    style: TextStyle(
                      fontSize: 10,
                      color: Colors.grey.shade600,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      );
    }

    return Align(
      alignment: Alignment.centerRight,
      child: ConstrainedBox(
        constraints: BoxConstraints(
          maxWidth: MediaQuery.of(context).size.width * 0.82,
        ),
        child: Container(
          margin: const EdgeInsets.only(bottom: AppSpacing.sm),
          padding: const EdgeInsets.all(AppSpacing.sm),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: const BorderRadius.only(
              topLeft: Radius.circular(12),
              topRight: Radius.circular(12),
              bottomLeft: Radius.circular(12),
              bottomRight: Radius.circular(2),
            ),
            border: Border.all(color: Colors.grey.shade300),
            boxShadow: [
              BoxShadow(
                color: Colors.black.withAlpha(8),
                blurRadius: 3,
                offset: const Offset(0, 1),
              ),
            ],
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text(
                'Siz',
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.bold,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                msg.body,
                style: const TextStyle(
                  fontSize: 14,
                  color: AppColors.textPrimary,
                ),
              ),
              if (msg.mediaUrls.isNotEmpty) ...[
                const SizedBox(height: 6),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: msg.mediaUrls.map((url) => _buildMediaThumbnail(url)).toList(),
                ),
              ],
              const SizedBox(height: 4),
              Align(
                alignment: Alignment.bottomRight,
                child: Text(
                  _formatDateTime(msg.createdAt),
                  style: TextStyle(
                    fontSize: 10,
                    color: Colors.grey.shade600,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildClosedBanner() {
    return Container(
      width: double.infinity,
      color: Colors.grey.shade200,
      padding: const EdgeInsets.all(AppSpacing.md),
      child: const SafeArea(
        top: false,
        child: Row(
          children: [
            Icon(Icons.lock_outline, color: Colors.grey, size: 20),
            SizedBox(width: AppSpacing.sm),
            Expanded(
              child: Text(
                'Bu sorun bildiriminiz çözümlenip kapatılmıştır.',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textSecondary,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildInputBar() {
    return SafeArea(
      top: false,
      child: Container(
        decoration: BoxDecoration(
          color: Theme.of(context).colorScheme.surface,
          border: Border(top: BorderSide(color: Colors.grey.shade300)),
        ),
        padding: const EdgeInsets.all(AppSpacing.sm),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (_selectedImage != null) ...[
              SizedBox(
                width: 72,
                height: 72,
                child: Stack(
                  children: [
                    ClipRRect(
                      borderRadius: BorderRadius.circular(8),
                      child: Image.memory(
                        _selectedImage!.bytes,
                        width: 64,
                        height: 64,
                        fit: BoxFit.cover,
                      ),
                    ),
                    Positioned(
                      top: 0,
                      right: 0,
                      child: GestureDetector(
                        onTap: () => setState(() => _selectedImage = null),
                        child: Container(
                          width: 20,
                          height: 20,
                          decoration: const BoxDecoration(
                            color: AppColors.error,
                            shape: BoxShape.circle,
                          ),
                          child: const Icon(Icons.close, size: 14, color: Colors.white),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: AppSpacing.xs),
            ],
            Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                IconButton(
                  icon: const Icon(Icons.camera_alt, color: AppColors.primary),
                  onPressed: _isSending ? null : _pickPhoto,
                  tooltip: 'Fotoğraf Ekle',
                ),
                Expanded(
                  child: TextField(
                    controller: _textController,
                    enabled: !_isSending,
                    minLines: 1,
                    maxLines: 4,
                    textCapitalization: TextCapitalization.sentences,
                    decoration: const InputDecoration(
                      hintText: 'Uzmana yanıt yazın...',
                      border: InputBorder.none,
                      contentPadding: EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                    ),
                  ),
                ),
                const SizedBox(width: AppSpacing.xs),
                IconButton(
                  icon: _isSending
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: AppColors.primary,
                          ),
                        )
                      : const Icon(Icons.send, color: AppColors.primary),
                  onPressed: _isSending ? null : _sendMessage,
                  tooltip: 'Gönder',
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildContent() {
    if (_isLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_errorMessage != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.error_outline, size: 48, color: AppColors.error),
              const SizedBox(height: AppSpacing.sm),
              const Text(
                'Bildirim detayları yüklenemedi',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.bold,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                _errorMessage!,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 13,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              ElevatedButton(
                onPressed: _loadCase,
                child: const Text('Tekrar Dene'),
              ),
            ],
          ),
        ),
      );
    }

    final detail = _detail;
    if (detail == null) {
      return const SizedBox.shrink();
    }

    return Column(
      children: [
        Expanded(
          child: RefreshIndicator(
            onRefresh: _loadCase,
            child: ListView(
              controller: _scrollController,
              padding: const EdgeInsets.all(AppSpacing.md),
              children: [
                _buildHeaderCard(detail),
                const SizedBox(height: AppSpacing.md),
                ...detail.messages.map((m) => _buildMessageBubble(m)),
              ],
            ),
          ),
        ),
        if (detail.status == CaseStatus.closed)
          _buildClosedBanner()
        else
          _buildInputBar(),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Vaka Detayı'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            tooltip: 'Yenile',
            onPressed: _loadCase,
          ),
        ],
      ),
      body: _buildContent(),
    );
  }
}
