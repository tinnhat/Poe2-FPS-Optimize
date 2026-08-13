# PoeRedux — PoE 2 Performance Toolkit

A customized, PoE 2-only build of PoeRedux focused on safer visual-performance patches, automatic backups, and a modern bilingual interface.

**Languages:** English · Tiếng Việt  
**Current release:** 1.2.2 Final  
**Platform:** Windows 10/11, x64

> This is an unofficial community tool. It is not affiliated with or endorsed by Grinding Gear Games.

## English

### Highlights

- Modern, scalable dark interface with English and Vietnamese support.
- Remembers the selected interface language between sessions.
- One-file, self-contained Windows executable.
- PoE 2 only; legacy PoE 1 menu and workflow are not included.
- Backs up original GGPK entries before writing and can restore them from the app.

### Available patches

- Camera zoom and full minimap reveal.
- Reduced monster non-combat effects.
- Reduced player-skill particles while preserving danger indicators.
- Safe residual-smoke removal for selected boss-death and harmless skill effects.
- Safe MTX particle and trail reduction.
- Corpse removal, including small monsters.
- Map-mod colors and always-visible monster HP.
- Atlas fog, environment fog, rain, cloud, environment-FX, shadow, and light reduction.
- Delirium fog fix covering environment fog, mirror-activation smoke, blur, shimmer, and player haze.
- Green-foliage material repair for data damaged by the retired clutter-removal option.

Gameplay-critical visuals such as poison clouds, ground effects, projectiles, AoE markers, and danger telegraphs are intentionally preserved by the safe reducers.

### Usage

1. Close Path of Exile 2.
2. Run the final `.exe`.
3. Select `Content.ggpk` for the standalone client, or the applicable bundle index `.bin` file for a bundle-based installation.
4. Select only the patches you want.
5. Click **Apply patches** and wait for the success message.

Use **Restore** to restore entries saved by PoeRedux. Backups are stored at:

```text
%LOCALAPPDATA%\PoeRedux\Backups\poe2.zip
```

After a game update, PackCheck, or Steam file verification, delete an obsolete PoeRedux backup before creating a new one.

### Repairing game files

- **Standalone client:** close the game and run `PackCheck.exe` from the Path of Exile 2 installation directory.
- **Steam:** use **Properties → Installed Files → Verify integrity of game files**.

File verification restores original game data and removes applied GGPK patches.

---

## Tiếng Việt

Đây là bản PoeRedux tùy biến chỉ dành cho PoE 2, tập trung vào các patch hình ảnh an toàn hơn, tự động sao lưu và giao diện Anh/Việt hiện đại.

### Điểm nổi bật

- Giao diện tối hiện đại, hỗ trợ tiếng Anh và tiếng Việt.
- Tự ghi nhớ ngôn ngữ đã chọn giữa các lần chạy.
- Chỉ cần một file `.exe`, không phải cài thêm .NET.
- Đã loại bỏ menu và quy trình dành cho PoE 1.
- Tự sao lưu dữ liệu GGPK gốc trước khi ghi và hỗ trợ khôi phục ngay trong app.

### Các patch hiện có

- Mở rộng camera và mở toàn bộ minimap.
- Giảm hiệu ứng phụ của quái.
- Giảm particle skill người chơi nhưng giữ dấu hiệu nguy hiểm.
- Xóa có chọn lọc khói sau khi boss chết và các trail skill vô hại.
- Giảm particle và trail MTX.
- Xóa xác của cả quái lớn và quái nhỏ.
- Tô màu mod bản đồ và luôn hiển thị HP quái.
- Giảm fog Atlas, fog môi trường, mưa, mây, environment FX, bóng đổ và ánh sáng.
- Xử lý fog Delirium, khói khi chạm mirror, blur, shimmer và lớp haze trên người chơi.
- Sửa material cây cối bị xanh do tùy chọn xóa vật thể cũ gây ra.

Các hiệu ứng quan trọng cho gameplay như poison cloud, ground effect, projectile, AoE marker và cảnh báo nguy hiểm được chủ động giữ lại.

### Cách sử dụng

1. Đóng Path of Exile 2.
2. Chạy file `.exe` final.
3. Chọn `Content.ggpk` nếu dùng standalone client, hoặc file bundle index `.bin` phù hợp nếu game dùng bundle riêng.
4. Chỉ chọn các patch bạn muốn dùng.
5. Bấm **Áp dụng patch** và chờ thông báo thành công.

Dùng **Khôi phục** để hoàn tác các file đã được PoeRedux sao lưu. Bản sao lưu nằm tại:

```text
%LOCALAPPDATA%\PoeRedux\Backups\poe2.zip
```

Sau khi game cập nhật, chạy PackCheck hoặc Verify trên Steam, hãy xóa backup PoeRedux cũ trước khi tạo backup mới.

### Khôi phục file game

- **Standalone client:** đóng game rồi chạy `PackCheck.exe` trong thư mục cài Path of Exile 2.
- **Steam:** chọn **Properties → Installed Files → Verify integrity of game files**.

Verify sẽ khôi phục dữ liệu game gốc và gỡ các patch đã ghi vào GGPK.

---

## Credits and attribution / Ghi công và nguồn tham khảo

This customized build exists thanks to the following open-source projects:

- **[Gineticus/PoeRedux](https://github.com/Gineticus/PoeRedux)** — the original PoeRedux application and the primary base of this project: WPF patcher architecture, UI workflow, backup/restore approach, and baseline patch implementations. The upstream repository is licensed under the **MIT License**.
- **[aianlinb/LibGGPK3](https://github.com/aianlinb/LibGGPK3)** — provides **LibGGPK3**, **LibBundle3**, and **LibBundledGGPK3**, used to read, edit, and save `Content.ggpk` and bundle index data. The upstream repository is licensed under **AGPL-3.0**.
- **[aianlinb/VisualGGPK2](https://github.com/aianlinb/VisualGGPK2)** — credited as the earlier LibGGPK2/VisualGGPK2 project from which LibGGPK3 was rewritten. It is part of the GGPK library lineage; this app directly depends on LibGGPK3 rather than VisualGGPK2.

Bản tùy biến này được phát triển dựa trực tiếp trên **Gineticus/PoeRedux** và sử dụng thư viện xử lý GGPK/bundle từ **aianlinb/LibGGPK3**. VisualGGPK2 được ghi công để thể hiện đầy đủ nguồn gốc phát triển của thư viện GGPK, không phải dependency trực tiếp của app.

All upstream authors retain copyright over their work. Third-party code and binaries remain subject to their respective upstream licenses. The repository's local `LICENSE` does not replace or weaken third-party license terms.

## Safety notes / Lưu ý an toàn

- Always close the game before patching or restoring.
- A game update can change data layouts; verify game files if a patch reports an unexpected layout.
- Keep gameplay-indicator effects enabled unless you understand the risk of removing them.
- This software is provided without warranty. Use it at your own risk and follow the game's applicable terms and policies.
