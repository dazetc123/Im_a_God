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
| 1 | **Divine foresight** (Thiên nhãn) | Trong 4 ngày, mọi threat nhắm vào colony đều được *báo trước* và đến muộn hơn 6/12/18/24 giờ (theo cấp). Xem mục [Tiên tri](#tiên-tri-và-storyteller). |
| 1 | Mending touch | Chữa vết thương, ưu tiên vết đang chảy máu. |
| 1 | Manna from heaven | Thả bữa ăn từ trời xuống, số lượng tăng theo số tín đồ. |
| 2 | Smite | Gọi sét đánh. Cấp càng cao càng nhiều tia sét. |
| 2 | Divine inspiration | Ban một inspiration ngẫu nhiên cho một tín đồ. |
| 2 | Divine revelation ⚑ (Thiên khải) | Giảm 25 nghi ngờ, tín đồ +5% certainty, có thể kéo kẻ dị giáo quay về. |
| 3 | Bountiful harvest ⚑ | Làm cây trồng trong vùng lớn nhanh. |
| 3 | Divine terror ⚑ | Kẻ địch trong vùng hoảng sợ bỏ chạy. |
| 3 | Resurrection ⚑ | Hồi sinh một tín đồ đã chết (xác chưa khô). |
| 3 | Humble a false god ⚑ (Thần chiến) | Giảm sức mạnh của một thần đối thủ (càng nhiều tín đồ càng giảm mạnh) và che chở colony khỏi thần đó trong 10 ngày. |
| 4 | **Incarnation** (Giáng thế) | Nhập vào một tín đồ trong 1 ngày: mạnh hơn, nhanh hơn, không đau, chịu ít sát thương. Khi thần rời đi, người đó bị kiệt sức trong 2 ngày. |

⚑ = cần nhà tiên tri dẫn phép.

Mở tab **Divinity** ở thanh dưới màn hình. Tab này có ba trang con: *Miracles* (phép lạ), *Rival gods* (thần đối thủ) và *Prophecies & faith* (tiên tri và nhật ký nghi ngờ).

## Tiên tri và storyteller

Thiên nhãn **không tạo thêm threat**, với bất kỳ storyteller nào (Cassandra, Phoebe, Randy, hay storyteller của mod khác dùng hệ thống incident của vanilla):

1. Storyteller tự quyết định bắn một sự kiện theo nhịp riêng của nó, như bình thường.
2. Ngay lúc đó (`Storyteller.TryFire`), mod giữ *chính sự kiện ấy* lại, đưa vào hàng đợi sự kiện của game với độ trễ 6–24 giờ, rồi gửi thư tiên tri.
3. Sự kiện được **ghi nhận vào story state ngay tại thời điểm quyết định**, y như thể nó đã xảy ra. Vì vậy các bộ đếm của storyteller (thời gian từ threat lớn gần nhất, `minRefireDays`, thống kê) giống hệt khi không có mod. Storyteller không "tưởng" là chưa có threat để bắn bù thêm.
4. Khi tới giờ, sự kiện xảy ra với đúng số điểm, faction, hướng đánh và chiến thuật đã báo. Nếu lúc đó không thể xảy ra nữa, game thử lại trong 1 ngày rồi bỏ. Tức là trường hợp xấu nhất là *ít* threat hơn, không bao giờ nhiều hơn.
5. Threat do quest tạo ra không bị hoãn.

| Cấp | Lời tiên tri cho biết |
|---|---|
| 1 | Loại sự kiện, nơi xảy ra, còn bao lâu. Báo trước 6 giờ. |
| 2 | Thêm faction địch, có champion của thần đối thủ dẫn đầu hay không, và tiên tri cả **dịch bệnh** (người và động vật). Báo trước 12 giờ. |
| 3 | Thêm **hướng tấn công** (bấm thư để nhảy tới điểm xuất phát), cách đến (đi bộ, drop pod…), chiến thuật (tấn công ngay, vây hãm, đào hầm…), sức mạnh. Báo trước 18 giờ. |
| 4 | Như cấp 3, báo trước 24 giờ. |

Can thiệp của thần đối thủ cũng được tiên tri. Nếu kịp dùng *Humble a false god* trước giờ đó, lời nguyền bị đẩy lùi.

## Khủng hoảng đức tin và dị giáo

Colony có chỉ số **nghi ngờ** (0–100), hiện trên thanh đỏ ở cột trạng thái. Di chuột vào để xem các sự kiện gần đây.

| Tăng nghi ngờ | Giảm nghi ngờ |
|---|---|
| Tín đồ chết +6, nhà tiên tri chết +12 | Tự giảm 1/ngày |
| Nghi lễ kém (chất lượng < 30%) +5 | Nghi lễ tốt (≥ 70%) −6 |
| Mỗi kẻ dị giáo trong colony +1/ngày | Mỗi phép lạ −2 (Thiên khải −25, Thần chiến −5) |
| **Thần im lặng**: đức tin ≥ 90% trần mà 3 ngày không làm phép, +3/ngày | Lời tiên tri ứng nghiệm −3 |
| Lời thì thầm của thần đối thủ +10 | Cầu nguyện −0.3, giết champion địch −10, kẻ dị giáo quay về −8 |

| Mức | Hậu quả |
|---|---|
| 25+ Lời xì xào | Đức tin thụ động −25%, tín đồ −2 mood |
| 50+ Khủng hoảng | Thụ động −50%, cầu nguyện −25%, certainty giảm 1%/ngày, −4 mood, **dị giáo** có thể nổ ra (MTB 10 ngày) |
| 75+ Ly giáo | Thụ động −75%, cầu nguyện −50%, certainty giảm 2%/ngày, −6 mood, dị giáo MTB 4 ngày |

**Dị giáo:** tín đồ có certainty thấp nhất (dưới 50%, không bao giờ là nhà tiên tri) bỏ đạo, chuyển sang thờ thần đối thủ mạnh nhất. Kẻ dị giáo làm nghi ngờ tăng và có thể đi cải đạo người khác bằng cơ chế cải đạo của vanilla. Để kéo họ về: cải đạo (chuyện trò, nghi lễ cải đạo) hoặc dùng *Divine revelation*.

## Thần đối thủ

Mỗi ideoligion của faction khác là một **thần đối thủ**. Sức mạnh của nó (0–1000) tăng mỗi ngày theo số settlement thờ nó. Chỉ thần của **faction thù địch** mới ra tay, tối đa 1 lần/6 ngày mỗi thần và 1 lần/4 ngày cho tất cả thần cộng lại:

| Sức mạnh | Can thiệp |
|---|---|
| 0+ | Lời thì thầm: +10 nghi ngờ, tín đồ −2% certainty |
| 150+ | Lời nguyền bạc lá (cây trồng) |
| 250+ | Cơn bão thịnh nộ: sét đánh vào khu Home |
| 300+ | **Champion**: raid của faction đó có thể có một kẻ được ban phước dẫn đầu (mạnh hơn, chịu ít sát thương). Giết champion thì thần đó mất 15% sức mạnh, bạn được +30 đức tin và −10 nghi ngờ. Đây không phải raid mới, chỉ là raid storyteller đã gửi được tăng cường. |
| 450+ | Gieo dị giáo vào tín đồ có certainty < 60% |

Can thiệp của thần đối thủ **là sự kiện thêm**, nằm ngoài storyteller. Bạn có thể chỉnh tần suất (0–200%) hoặc tắt hẳn trong Mod Settings.

**Mod settings:** chỉnh hệ số nhận đức tin, hệ số giá phép, bật/tắt Thiên nhãn, bật/tắt khủng hoảng đức tin, và mức hoạt động của thần đối thủ (0 = tắt).

## Cấu trúc thư mục

```
About/                 About.xml
1.6/Assemblies/        ImAGod.dll (đã build sẵn)
Defs/                  thần cấp, phép lạ, bàn thờ, job, thought, hediff, main tab
Languages/English/     chuỗi giao diện (Keyed)
Textures/ImAGod/       icon phép, icon tab, texture bàn thờ
Source/ImAGod/         mã nguồn C#
  Core/                GameComponent_Divinity (trạng thái), Def, settings, tiện ích
  Crisis/              nghi ngờ, dị giáo, thought khủng hoảng đức tin
  Rivals/              thần đối thủ, can thiệp, champion
  Prophecy/            Thiên nhãn: giữ và báo trước sự kiện
  Powers/              DivinePowerWorker và các phép
  Prayer/              JobDriver_Pray, JoyGiver_Pray
  Patches/             Harmony: Storyteller.TryFire, IncidentQueue, nghi lễ, Pawn.Kill
  UI/                  MainTabWindow_Divinity
```

### Thêm phép mới

1. Viết một class kế thừa `ImAGod.DivinePowerWorker` và override `Apply` (có thể override thêm `CanApplyOn` và `EffectSummary`).
2. Thêm một `<ImAGod.DivinePowerDef>` vào `Defs/DivinePowerDefs.xml`. Các giá trị số (giá, cấp, cooldown, bán kính…) đều chỉnh được trong XML, không cần build lại.

Can thiệp mới của thần đối thủ cũng làm tương tự: kế thừa `ImAGod.RivalInterventionWorker` và thêm `<ImAGod.RivalInterventionDef>` vào `Defs/RivalInterventionDefs.xml`.

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
- [x] Tiên tri chi tiết: hướng tấn công, cách đến, chiến thuật, champion, dịch bệnh
- [x] Khủng hoảng đức tin và dị giáo
- [x] Thần đối thủ: can thiệp, champion, Thần chiến
- [ ] Bản dịch tiếng Việt
- [ ] Ảnh preview cho Steam Workshop
