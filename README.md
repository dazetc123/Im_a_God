# I'm a God — RimWorld mod

Bạn không còn chỉ là người quan sát: **bạn là vị thần mà colony tôn thờ**. Sức mạnh của bạn tăng theo số lượng tín đồ và lượng đức tin họ dâng lên.

- **Phiên bản:** RimWorld 1.6
- **Bắt buộc:** DLC Ideology và [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- **Hướng thiết kế:** phương án D (kết hợp), thiên về cân bằng và nhập vai

## Cơ chế cốt lõi

| Khái niệm | Ý nghĩa |
|---|---|
| **Tín đồ** | Colonist theo ideoligion chính của colony. Nô lệ tính 0.5. |
| **Đức tin** | Tiền tệ để thi triển phép lạ. Trần tối đa = `faithCapBase + faithCapPerFollower × tín đồ` (tăng theo thần cấp). |
| **Thần cấp** | Lấy cấp cao nhất mà bạn đạt đủ *cả* số tín đồ *lẫn* tổng đức tin từng nhận. |
| **Áp lực thần lực** (strain) | Mỗi phép +1, giảm 0.5/ngày. Mỗi điểm làm phép đắt thêm 15%. |
| **Nhà tiên tri** | Moral guide của ideoligion (nếu không có thì dùng leader). Phép lớn phải đi qua người này, và họ phải chịu hediff *divine channeling*. Khi bị *overwhelmed* thì không thể dẫn phép tiếp. |

### Thần cấp

| Cấp | Tên | Tín đồ | Tổng đức tin |
|---|---|---|---|
| 1 | Nascent god | 1 | 0 |
| 2 | Revered god | 5 | 400 |
| 3 | Mighty god | 9 | 2000 |
| 4 | Exalted god | 14 | 6000 |

### Nguồn đức tin

- **Sùng kính thụ động:** mỗi ngày, `certainty × hệ số theo cấp` cho mỗi tín đồ.
- **Cầu nguyện ở Divine Shrine** (tab Architect → Joy): là một dạng giải trí *Meditative*, được khoảng 4 đức tin/giờ × certainty. Sau khi cầu nguyện, pawn được +1% certainty và một thought tích cực.
- **Nghi lễ của ideoligion:** được `(15 + 6 × số tín đồ tham dự) × (0.25 + chất lượng)` đức tin.
- **Chứng kiến phép lạ:** tín đồ ở gần được +4% certainty và +6 mood. Người khác đạo (thuộc colony hoặc tù nhân) bị giảm certainty với đạo của mình và có thể cải đạo.

### Phép lạ

| Cấp | Phép | Mô tả |
|---|---|---|
| 1 | **Divine foresight** (Thiên nhãn) | Trong 4 ngày, mọi threat nhắm vào colony đều được *báo trước* bằng một lời tiên tri và đến muộn hơn 6/12/18/24 giờ (theo cấp). Không tạo thêm threat nào, nên lời tiên tri luôn đúng. Từ cấp 2 biết được faction địch, từ cấp 3 biết được sức mạnh. |
| 1 | Mending touch | Chữa vết thương, ưu tiên vết đang chảy máu. |
| 1 | Manna from heaven | Thả bữa ăn từ trời xuống, số lượng tăng theo số tín đồ. |
| 2 | Smite | Gọi sét đánh. Cấp càng cao càng nhiều tia sét. |
| 2 | Divine inspiration | Ban một inspiration ngẫu nhiên cho một tín đồ. |
| 3 | Bountiful harvest ⚑ | Làm cây trồng trong vùng lớn nhanh. |
| 3 | Divine terror ⚑ | Kẻ địch trong vùng hoảng sợ bỏ chạy. |
| 3 | Resurrection ⚑ | Hồi sinh một tín đồ đã chết (xác chưa khô). |
| 4 | **Incarnation** (Giáng thế) | Nhập vào một tín đồ trong 1 ngày: mạnh hơn, nhanh hơn, không đau, chịu ít sát thương. Khi thần rời đi, người đó bị kiệt sức trong 2 ngày. |

⚑ = cần nhà tiên tri dẫn phép.

Mở tab **Divinity** ở thanh dưới màn hình để xem trạng thái và thi triển phép.

**Mod settings:** chỉnh hệ số nhận đức tin, hệ số giá phép, và bật/tắt Thiên nhãn.

## Cấu trúc thư mục

```
About/                 About.xml
1.6/Assemblies/        ImAGod.dll (đã build sẵn)
Defs/                  thần cấp, phép lạ, bàn thờ, job, thought, hediff, main tab
Languages/English/     chuỗi giao diện (Keyed)
Textures/ImAGod/       icon phép, icon tab, texture bàn thờ
Source/ImAGod/         mã nguồn C#
  Core/                GameComponent_Divinity (trạng thái), Def, settings, tiện ích
  Powers/              DivinePowerWorker và các phép
  Prayer/              JobDriver_Pray, JoyGiver_Pray
  Patches/             Harmony: tiên tri (Storyteller.TryFire), đức tin từ nghi lễ
  UI/                  MainTabWindow_Divinity
```

### Thêm phép mới

1. Viết một class kế thừa `ImAGod.DivinePowerWorker` và override `Apply` (có thể override thêm `CanApplyOn` và `EffectSummary`).
2. Thêm một `<ImAGod.DivinePowerDef>` vào `Defs/DivinePowerDefs.xml`. Các giá trị số (giá, cấp, cooldown, bán kính…) đều chỉnh được trong XML, không cần build lại.

## Build

Cần .NET SDK 8 trở lên (không cần cài RimWorld, vì dùng reference assemblies `Krafs.Rimworld.Ref`):

```bash
cd Source/ImAGod
dotnet build -c Release
```

File DLL được xuất ra `1.6/Assemblies/ImAGod.dll`.

## Cài đặt

Copy (hoặc tạo symlink) cả thư mục repo vào `RimWorld/Mods/ImAGod`. Bật Harmony, Ideology rồi đến I'm a God.

## Lộ trình

- [x] **MVP:** đức tin, tín đồ, thần cấp, tab Divinity, bàn thờ và cầu nguyện, đức tin từ nghi lễ, 9 phép lạ, Thiên nhãn, nhà tiên tri, Giáng thế
- [ ] Tiên tri chi tiết hơn: hướng tấn công, nhóm sự kiện không phải threat (bệnh dịch, thiên tai)
- [ ] Khủng hoảng đức tin: tín đồ chết hoặc lời cầu nguyện không được đáp → dị giáo
- [ ] Thần của faction khác làm đối thủ (thần chiến)
- [ ] Bản dịch tiếng Việt
- [ ] Ảnh preview cho Steam Workshop
