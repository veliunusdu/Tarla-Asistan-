import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import 'package:mobile/features/location/domain/tarla_location.dart';

const _openStreetMapTileUrl = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';
const _turkiyeCenter = TarlaLocation(latitude: 39.0, longitude: 35.0);

typedef FieldLocationMapBuilder =
    Widget Function(
      BuildContext context,
      TarlaLocation center,
      ValueChanged<TarlaLocation> onLocationSelected,
      VoidCallback onLoadError,
    );

class FieldLocationPickerScreen extends StatefulWidget {
  const FieldLocationPickerScreen({
    super.key,
    this.initialLocation,
    this.mapBuilder,
  });

  final TarlaLocation? initialLocation;
  final FieldLocationMapBuilder? mapBuilder;

  @override
  State<FieldLocationPickerScreen> createState() =>
      _FieldLocationPickerScreenState();
}

class _FieldLocationPickerScreenState extends State<FieldLocationPickerScreen> {
  late TarlaLocation? _selectedLocation = widget.initialLocation;
  bool _hasMapLoadError = false;

  void _selectLocation(TarlaLocation location) {
    setState(() => _selectedLocation = location);
  }

  void _showMapLoadError() {
    if (!_hasMapLoadError) {
      setState(() => _hasMapLoadError = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final center = _selectedLocation ?? _turkiyeCenter;

    return Scaffold(
      appBar: AppBar(title: const Text('Tarla konumunu seç')),
      body: _hasMapLoadError
          ? _MapLoadError(onBack: () => Navigator.of(context).pop())
          : Column(
              children: [
                Expanded(
                  child:
                      widget.mapBuilder?.call(
                        context,
                        center,
                        _selectLocation,
                        _showMapLoadError,
                      ) ??
                      _OpenStreetMap(
                        center: center,
                        selectedLocation: _selectedLocation,
                        onLocationSelected: _selectLocation,
                        onLoadError: _showMapLoadError,
                      ),
                ),
                SafeArea(
                  top: false,
                  minimum: const EdgeInsets.all(16),
                  child: SizedBox(
                    width: double.infinity,
                    child: ElevatedButton(
                      onPressed: _selectedLocation == null
                          ? null
                          : () => Navigator.of(context).pop(_selectedLocation),
                      child: const Text('Bu konumu kullan'),
                    ),
                  ),
                ),
              ],
            ),
    );
  }
}

class _OpenStreetMap extends StatelessWidget {
  const _OpenStreetMap({
    required this.center,
    required this.selectedLocation,
    required this.onLocationSelected,
    required this.onLoadError,
  });

  final TarlaLocation center;
  final TarlaLocation? selectedLocation;
  final ValueChanged<TarlaLocation> onLocationSelected;
  final VoidCallback onLoadError;

  @override
  Widget build(BuildContext context) {
    return FlutterMap(
      options: MapOptions(
        initialCenter: LatLng(center.latitude, center.longitude),
        initialZoom: 6,
        onTap: (_, point) => onLocationSelected(
          TarlaLocation(latitude: point.latitude, longitude: point.longitude),
        ),
      ),
      children: [
        TileLayer(
          urlTemplate: _openStreetMapTileUrl,
          userAgentPackageName: 'com.tarlaasistani.pilot',
          errorTileCallback: (_, _, _) => onLoadError(),
        ),
        if (selectedLocation case final location?)
          MarkerLayer(
            markers: [
              Marker(
                point: LatLng(location.latitude, location.longitude),
                width: 48,
                height: 48,
                child: const Icon(
                  Icons.location_pin,
                  color: Colors.red,
                  size: 48,
                ),
              ),
            ],
          ),
        const RichAttributionWidget(
          attributions: [TextSourceAttribution('© OpenStreetMap contributors')],
        ),
      ],
    );
  }
}

class _MapLoadError extends StatelessWidget {
  const _MapLoadError({required this.onBack});

  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('Harita yüklenemedi. Lütfen bağlantınızı kontrol edin.'),
            const SizedBox(height: 16),
            OutlinedButton(onPressed: onBack, child: const Text('Geri dön')),
          ],
        ),
      ),
    );
  }
}
