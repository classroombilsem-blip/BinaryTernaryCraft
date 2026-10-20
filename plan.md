# 3B Voksel Tabanlı Ternary Bilgisayar ve Mekanik Simülasyon Motoru (Plan & Mimari Spesifikasyonu)

## 1. Proje Özeti ve Nihai Tasarım Kararları
- **Tür:** 3B Voksel Tabanlı Ternary (-1, 0, +1) Mantık ve Mekanik Simülasyon Motoru.
- **Hedef:** 1.000 - 10.000 TPS simülasyon hızı, 60-144+ FPS akıcı render.
- **Mimari:** Saf Veri Odaklı Tasarım (Data-Oriented Design / DOD), sıfır GC tahsisi, `struct` & flat arrays, `unsafe` bloklar, pointer chasing ve OOP blok sınıflarından kaçınma.
- **Çalışma Zamanı & Diller:** C# (.NET 8.0/9.0/10), `Silk.NET.OpenGL` (OpenGL 4.5+ Direct State Access ve SSBO), `Silk.NET.Windowing`, `Silk.NET.Input`, `Silk.NET.OpenGL.Extensions.ImGui` (`ImGui.NET`), `StbImageSharp` (Hafif ve saf C# PNG atlas yükleyici).

---

## 2. Dünya ve Bellek Düzeni (Kesinleşen Tasarım)
- **Chunk Boyutu:** 16x16x16 = 4096 blok.
- **Varsayılan Dünya Grid Boyutu:** 16 x 16 x 12 Chunk (256 x 256 x 192 Blok) — Konfigüre edilebilir sabit boyutlu grid.
- **İndeksleme:** 
  - Chunk içi yerel indeks: `localIndex = x + (y * 16) + (z * 256)`
  - Global SSBO mantık indeksi: `globalIndex = (chunkY * GridX * GridZ + chunkZ * GridX + chunkX) * 4096 + localIndex`
- **Flat Array Veri Yapıları:**
  - `ushort[] BlockId`: Blok kimliği (Hava, RGB Kablo, Ternary Kapılar, Zamanlayıcılar, Mekanik Şaft/Dişli vb.)
  - `sbyte[] TritState`: Mantıksal durum (-1: Kırmızı / R, 0: Yeşil / G, +1: Mavi / B)
  - `byte[] BlockMeta`: Yön (0..5: -X, +X, -Y, +Y, -Z, +Z) ve mod bitleri
  - `ushort[] BlockTimerConfig`: Zamanlayıcı/uzatıcı tick süresi konfigürasyonu
  - `int[] NetworkId`: Mantık kablo ağı (`WireNetId`) veya mekanik ağ (`KineticNetId`) referansı

---

## 3. Render Hattı ve Teknikler

### Teknik A: Sinyal Değişiminde Mesh Yenilememe (SSBO Dinamik Renklendirme)
- Blokların renk/mantık değişimi (`-1`, `0`, `+1`) Chunk mesh'ini invalidate ETMEZ.
- Her vertex `uint GlobalLogicIndex` taşır.
- Fragment veya Vertex Shader, SSBO (`layout(std430, binding = 0) readonly buffer StateBuffer { int States[]; }`) üzerinden bloğun değerini okuyup anında renklendirir:
  - `-1`: Kırmızı (R)
  - `0`: Yeşil (G - Nötr)
  - `+1`: Mavi (B)

### Teknik B: Bedrock `.geo.json` Parser & Kinetik Ayrıştırma
- Blockbench `minecraft:geometry` formatı okunur.
- Koordinatlar 16 piksel ölçeğinden OpenGL 1x1x1 voksel ölçeğine (`1.0f / 16.0f`) dönüştürülür.
- **Ayrıştırma Kuralı:**
  1. `kinetic_` önekli veya yalnızca hareketli kemikler barındıran modeller: Tümü Kinetik Mesh.
  2. Kısmi modeller (`casing`, `base` vs. `shaft`, `gear`): Sabit kemikler statik Chunk mesh'ine eklenir; dönen kemikler GPU Instanced çizim kuyruğuna aktarılır.
- Shader içinde `uTime * Speed * GearRatio` ile `pivot` etrafında GPU rotasyonu.

### Teknik C: Doku Atlası ve Materyal
- `StbImageSharp` ile yüklenen Texture Atlas (PNG).
- Standart blok yüzeyleri ve `.geo.json` UV'leri bu atlas üzerinden eşlenir.
- SSBO Emissive katmanı ile kablolar ve mantık pikselleri dinamik olarak parlar.

---

## 4. Kullanıcı Etkileşimi (UX)
1. **Raycast & Seçim:**
   - Sol Tık: Blok kırma (Air atama, mesh güncelleme).
   - Sağ Tık: Blok koyma (Hedef yüzeye yeni blok ekleme).
2. **Trit Source Hızlı Seçici (Radial/Bar Slider):**
   - Trit Source bloğuna sağ tık basılı tutulduğunda fare imlecinin yanında 3 kademeli mini seçim çubuğu açılır:
     - Sola kaydırınca `-1` (Kırmızı)
     - Ortada `0` (Yeşil)
     - Sağa kaydırınca `+1` (Mavi)
     - Tık bırakılınca seçilen durum bloğa atanır.
3. **ImGui Inspector & Panel:**
   - Hedeflenen bloğun anlık detayları (ID, State, Timer, Ağ Kimliği).
   - Darbe/zamanlayıcı süreleri için sayısal ayar alanları.
   - TPS Kontrol Slider'ı (1 - 10.000 TPS) ve Pause/Step-Tick butonları.

---

## 5. Uygulama Adımları
- [x] **Tasarım & Planlama:** Grill-me oturumu tamamlandı, kararlar netleşti.
- [x] **Adım 1:** .NET Projesi (`AllowUnsafeBlocks=true`), Silk.NET Pencereleme, OpenGL 4.5 Context, Input ve FPS Kamera, Raycasting.
- [x] **Adım 2:** Flat-Array Chunk sistemi, `GeoJsonModelLoader`, Chunk Mesher, Texture Atlas, SSBO Dinamik Renklendirme ve GPU Instancing Shader'ları, Dahili test modelleri.
- [x] **Adım 3:** `SimulationThread` (1.000 - 10.000 TPS, Stopwatch döngüsü), Çift tamponlu sıfır tahsisatlı `FastQueue` (`CurrentTickQueue` & `NextTickQueue`), `ScheduledTickQueue` (`PriorityQueue<ScheduledLogicEvent, long>`), `WireNet` BFS çözücüsü, Balanced Ternary kapıları (NOT, CYCLE, ANTI-CYCLE, MIN, MAX, SUM, CONSENSUS) ve Aeronautics zamanlayıcı blokları (PulseGenerator, PulseExtender, ToggleLatch, TritLatch).
- [x] **Adım 4:** `KineticNetwork` çözücüsü (BFS flood-fill mekanik ağ gruplama, ters yön dişli kenetlenmesi `Ratio = -ParentRatio`, $O(1)$ çalışma zamanı), `TernaryMotor` (+1: +BaseRPM, 0: 0 RPM, -1: -BaseRPM doğrudan SSBO Binding 1 güncellemesi), `TernaryPiston` 3 kademeli aktüatör (-1: 0 blok, 0: 1 blok, +1: 2 blok dünyada 12 bloğa kadar fiziksel itme) ve GPU Instanced Senkronizasyonu.
- [x] **Adım 5:** `ImGui` Arayüzü (Canlı 128-tick Lojik Analizör / Osiloskop dairesel tamponu, Dalga Formu grafiği, Pin/Unpin mekanizması, 1-10.000 TPS logaritmik hız slider'ı ve 1/10/100/1K/10K hızlı butonları, Pause/Resume ve Tek Tick ilerletme, Dinamik Blok Denetleyicisi ve Canlı Zamanlayıcı/Kaynak Ayarlayıcısı, [P], [N], [F3] klavye kısayolları).
