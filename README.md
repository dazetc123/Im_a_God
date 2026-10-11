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

## Con đường thần thánh và Lĩnh vực

Khi bắt đầu game, hoặc khi tải một save chưa chọn, mod hỏi bạn là thần gì:

1. **Con đường** (chọn một lần, vĩnh viễn): **Sáng tạo** hoặc **Hỗn mang**.
2. **Lĩnh vực** đầu tiên thuộc con đường đó. Lên cấp 2 được nhận thêm lĩnh vực thứ hai, lên cấp 4 được thêm lĩnh vực thứ ba (cùng con đường).

| Sáng tạo | Hỗn mang |
|---|---|
| Mùa màng · Bảo hộ · Chiến tranh · Xây dựng · Chế tạo · Sự sống | Hủy diệt · Điên loạn · Dục vọng · Khát máu · Dịch bệnh |

Mỗi lĩnh vực có:
- **Phước lành**: hediff cho mọi tín đồ, mạnh dần theo thần cấp. Ví dụ Mùa màng tăng tốc độ làm cây và sản lượng; Khát máu tăng sát thương cận chiến nhưng dễ ẩu đả.
- **Lễ vật**: việc tín đồ làm trong lĩnh vực đó dâng đức tin. Mùa màng là thu hoạch và gieo trồng; Chiến tranh là hạ gục kẻ thù; Chế tạo là chế đồ, nấu ăn, nghiên cứu; Điên loạn là mỗi cơn break trên bản đồ; Dục vọng là lovin', tiệc tùng…
- **Cây phép của thần**: 5 phép trên 3 hàng, giá 1/2/3 Điểm thần lực. Mỗi phép cần *một* phép bất kỳ ở hàng trên.
- **Cây kỹ năng Á thần**: 3 kỹ năng, mở ở cấp Á thần 1/4/7.

**Điểm thần lực:** nhận theo thần cấp cao nhất từng đạt (cấp 1: 2 điểm, cấp 2: 4, cấp 3: 7, cấp 4: 10), cộng 1 điểm cho mỗi 1000 đức tin từng nhận (tối đa thêm 10).

Phép chung, ai cũng có: Thiên nhãn, Mana trời, Thiên khải, Thần chiến, Hồi sinh, Giáng thế, Thăng hoa. Các phép đặc thù cũ nay thuộc về lĩnh vực: Chữa lành → Sự sống, Ban cảm hứng → Chế tạo, Sét → Chiến tranh và Hủy diệt, Bội thu → Mùa màng, Thịnh nộ → Điên loạn.

## Á thần

- Phép **Thăng hoa** (cấp 1, 300 đức tin, cần nhà tiên tri) nâng một tín đồ có certainty ≥ 80% thành Á thần. Số Á thần tối đa bằng thần cấp.
- **Á thần nhận được:** mọi kỹ năng +8, mood +30, đau chỉ còn 30%, sát thương nhận vào ×0.5, giáp da (sắc +40%, cùn +30%, nhiệt +30%), né cận chiến +20, trúng +10, bắn +10, mọi năng lực cơ thể +20%, hồi máu ×4, chảy máu ×0.3, miễn dịch ×1.6, học nhanh ×1.5, ít đói và ít buồn ngủ hơn.
- **Kinh nghiệm:** Á thần lên cấp (tối đa 10) theo thời gian, theo đức tin mà việc họ làm dâng lên, và theo đức tin họ tiêu. Mỗi cấp cho 1 điểm để học kỹ năng trong trang *Demigods*.
- **Trực thuộc thần:** kỹ năng của Á thần là nút bấm như psycast nhưng **tiêu đức tin của bạn**, và chỉ học được từ các lĩnh vực bạn đã nhận.
- Á thần chết thì nghi ngờ +15. Nếu bỏ đạo hoặc rời colony, họ mất thần lực.

## Đời sống tôn giáo

- **Lời cầu xin:** thỉnh thoảng một tín đồ cầu xin một điều cụ thể: chữa bệnh, thức ăn, che chở khi bị raid, một dấu hiệu khi đang hoài nghi, hoặc chấm dứt thời tiết xấu.
  - Đáp lại đúng loại phép trước hạn chót: +đức tin, −8 nghi ngờ, người đó +10% certainty và +8 mood.
  - Bỏ qua: +6 nghi ngờ, −6 mood.
  - Nếu việc được giải quyết bằng cách thường (thuốc, nấu ăn, thời tiết tự hết) thì không thưởng cũng không phạt.
- **Thiên luật:** ban một điều răn có thời hạn từ trang *Prophecies & faith*, mỗi lần một điều, cách nhau 5 ngày.
  - Loại *cấm*: bị phá ngay khi có một tín đồ vi phạm. Ví dụ: kiêng thịt, kiêng chất kích thích, không giết người.
  - Loại *nhiệm vụ*: hoàn thành khi đạt chỉ tiêu. Ví dụ: cầu nguyện, xây dựng, thu hoạch, đổ máu, hoan lạc, phá dỡ.
  - Giữ được: được đức tin và mood. Thất bại: tăng nghi ngờ.
- **Ngày lễ thánh:** mỗi lĩnh vực có một ngày lễ trong năm. Hôm đó nghi lễ ×2 đức tin, cầu nguyện ×1.5, tín đồ +6 mood.
- **Bệ tế máu (chỉ Hỗn mang):** chọn bệ, chọn tù nhân hoặc gia súc để dâng.
  - Nhà tiên tri, hoặc một tín đồ khác, thực hiện nghi lễ.
  - Tù nhân dâng 90–220 đức tin, động vật tính theo kích thước.

## Hiệu ứng phép lạ

Mỗi phép có cột sáng rơi xuống mục tiêu, vòng sáng lan theo bán kính, tia lửa và âm thanh thần thánh. Màu vàng cho Sáng tạo, màu đỏ thẫm cho Hỗn mang. Phép lớn còn có sấm và rung màn hình.

## Đặt lại và Á thần

- **Tái hiến:** quên mọi phép đã mở và nhận lại điểm thần lực. Giá 150 + 50 × thần cấp đức tin.
- **Đặt lại kỹ năng Á thần:** 100 đức tin.
- **Danh hiệu Á thần** theo lĩnh vực họ học nhiều nhất: Thánh kỵ sĩ, Thánh y, Kẻ gặt máu, Tiên tri điên…
- Á thần có **vầng hào quang** sáng trên đầu.
- **Thánh khí:** mỗi Á thần được ban một lần, giá 250 đức tin, chất lượng Huyền thoại. Sáng tạo nhận *Kiếm Mặt Trời* (đốt cháy, tăng tốc chạy); Hỗn mang nhận *Kiếm Máu* (sát thương cao, né đòn).

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
| 700+ | **Hiện thân giáng thế**: thần đối thủ gửi hiện thân (Seraphim hoặc Tiamat, xem dưới) kèm một đoàn raid hộ tống (50% sức raid thường) |

Can thiệp của thần đối thủ **là sự kiện thêm**, nằm ngoài storyteller. Bạn có thể chỉnh tần suất (0–200%) hoặc tắt hẳn trong Mod Settings.

**Mod settings:** chỉnh hệ số nhận đức tin, hệ số giá phép, bật/tắt Thiên nhãn, bật/tắt khủng hoảng đức tin, và mức hoạt động của thần đối thủ (0 = tắt).

## Sứ giả thần thánh và Chiến tranh hiện thân

Từ **thần cấp 3**, nhà tiên tri có thể gọi **sứ giả lớn** của thần xuống trần (800 đức tin, hồi 12 ngày, ở lại 3 ngày, mỗi lúc chỉ một con):

- **Con đường Sáng tạo → Seraphim**: thiên thần sáu cánh lửa trắng, cánh đầy mắt, hào quang, kiếm lửa và giáo thánh. Sức mạnh kiểu quả cầu nocto (Anomaly) nhưng mạnh hơn.
- **Con đường Hỗn mang → Tiamat**: rồng năm đầu, mẹ của quái vật. Mỗi đầu một màu, mỗi màu một hơi thở.

Sứ giả **không cần draft**: nó tự đánh và tự dùng kỹ năng khi đáng dùng. Chọn nó để ra lệnh qua các nút:

- dùng từng kỹ năng (chọn mục tiêu);
- bật/tắt tự dùng kỹ năng;
- canh gác một chỗ (chỉ đánh kẻ đến gần) hoặc đánh tự do;
- ra lệnh tấn công một mục tiêu;
- cho về trời sớm.

Khi bị hạ gục, nó trở về trời và không để lại xác.

| Seraphim | Tác dụng | Hồi |
|---|---|---|
| Cung lửa thánh | Tia lửa trắng dài 30 ô, xuyên mọi kẻ địch trên đường, gây cháy | 7 giây |
| Cột phán xét | Đánh dấu một chỗ; 1,5 giây sau cột lửa thánh giáng xuống, nổ bán kính 3,9 | 15 giây |
| Dịch chuyển thánh | Biến mất rồi hiện ra cạnh mục tiêu (50 ô), làm choáng kẻ quanh chỗ đáp | 12 giây |
| Trục xuất | Ném tối đa 5 kẻ địch quanh nó văng xa 12–25 ô, choáng 3 giây | 25 giây |
| Thánh ca Seraphim | Hồi máu đồng minh trong 12 ô, ban khiên giảm 50% sát thương trong 25 giây | 40 giây |
| Gọi Kẻ Canh Giữ | Triệu hồi 3 **Winged Beholder** (mắt có cánh) chiến đấu nửa ngày, tối đa 6 con | 2 giờ trong game |

**Winged Beholder**: con mắt lớn có cánh. Nó bắn **tia laser từ mắt** xuyên thẳng hàng (3 giây/lần) và có **ánh nhìn tê liệt** làm choáng một kẻ địch.

| Tiamat | Tác dụng | Hồi |
|---|---|---|
| Hơi thở đỏ thẫm | Hình nón lửa 13 ô, đốt cháy mọi thứ | 10 giây |
| Hơi thở lam | Sét nhảy qua 5 kẻ địch, gây choáng | 12 giây |
| Hơi thở lục | Mây độc hình nón, gây nhiễm độc (toxic buildup) | 18 giây |
| Hơi thở hắc thạch | Tia axit đen xuyên thẳng hàng, xuyên giáp rất cao | 10 giây |
| Hơi thở bạc | Hình nón băng giá, gây bỏng lạnh và làm chậm 50% | 14 giây |
| Gầm hỗn mang | Choáng mọi kẻ địch trong 14 ô; 45% bỏ chạy hoảng loạn | 45 giây |
| Mẹ của quái vật | Đẻ 2 **Mušḫuššu** (rồng rắn: chân sư tử, móng đại bàng, đuôi bọ cạp có độc), chiến đấu 1 ngày, tối đa 4 con | 2 giờ trong game |

Đòn đánh thường của sứ giả có xuyên giáp cao (vanilla 100–130%).

**Combat Extended**: bản patch riêng tự bật khi có CE:

- đòn cận chiến xuyên **30–40 mm sharp** và **160–180 MPa blunt**;
- giáp, né, đỡ, chí mạng tính theo đơn vị CE;
- kỹ năng dùng giá trị `armorPenetrationCE`.

**Chiến tranh hiện thân**: thần đối thủ thù địch từ **700 sức mạnh** có thể gửi hiện thân của nó kèm raid hộ tống.

- Thần của tôn giáo tàn bạo (Raider, Supremacist, PainIsVirtue, Cannibal…) hiện thân thành Tiamat; còn lại là Seraphim.
- Hiện thân mạnh hơn sứ giả thường: nhận ít sát thương hơn, đánh mạnh hơn.
- Thiên nhãn báo trước được, và bị đẩy lùi nếu thần đó đang bị hạ nhục.
- Giết được hiện thân:
  - thần đó mất 40% sức mạnh và bị hạ nhục 15 ngày;
  - bạn nhận +150 + 50×cấp đức tin và −15 nghi ngờ;
  - mọi tín đồ được +10 tâm trạng trong 6 ngày.
- Sứ giả của bạn đánh được hiện thân: thần đấu thần.

## Truyền giáo

Cho một **đoàn caravan có tín đồ** đến settlement của faction trung lập hoặc thân thiện, rồi bấm **"Preach the faith"**. Đoàn phải ở lại **2 ngày**.

Tỉ lệ thành công:

- **tăng** theo Social cao nhất của các nhà truyền giáo, số người, thần cấp, quan hệ với faction;
- **+15%** nếu có Á thần đi cùng, **+10%** nếu có nhà tiên tri;
- **giảm** nếu thần của làng đó mạnh, hoặc colony đang khủng hoảng đức tin.

Kết quả:

| Kết quả | Hiệu ứng |
|---|---|
| Thành công | Làng tin thêm 25–40% (+10% nếu có Á thần), +30 + 10×cấp đức tin, +5 quan hệ, thần đối thủ −20 sức mạnh, nhà truyền giáo được +6 tâm trạng |
| Thất bại | +5% niềm tin, −2 quan hệ |
| Thảm hoạ | Nhà truyền giáo bị đánh, −12 quan hệ, +4 nghi ngờ |

Khi niềm tin đạt 100%, **settlement được cải đạo**:

- +6 đức tin mỗi ngày;
- không còn nuôi thần cũ (thần cũ −60 sức mạnh);
- +15 quan hệ.

Khi **một nửa số settlement** của một faction đã cải đạo, **cả faction đổi sang ideoligion của bạn**: thần cũ −150 sức mạnh, +25 quan hệ, −15 nghi ngờ. Từ đó người của faction đó sinh ra đã là tín đồ của bạn.

Mỗi settlement phải chờ 3 ngày giữa hai lần truyền giáo. Tiến độ hiện ở trang Faith của tab Divinity và khi chọn settlement trên bản đồ thế giới.

## Dev mode (để test)

Bật **Development mode** trong Options → mở nút **Debug actions** (biểu tượng bánh răng trên thanh dev) → gõ tìm `I'm a God`. Mọi công cụ đều nằm trong mục đó:

| Nhóm | Công cụ |
|---|---|
| Đức tin & cấp | Faith +100 / +1000, đầy đức tin, về 0; **ép thần cấp** 0–4 (hoặc trả về tự động); +5 điểm thần; **phép miễn phí** (không tốn đức tin, không cooldown, bật/tắt); xoá cooldown, căng thẳng và trạng thái truyền thần của nhà tiên tri; ghi toàn bộ trạng thái ra log |
| Con đường | Đặt lại con đường và lĩnh vực (mở lại hộp thoại chọn); nhận lĩnh vực bất kỳ bỏ qua giới hạn ô; mở khoá mọi phép của các lĩnh vực đang có; quên hết phép miễn phí |
| Khủng hoảng & đối thủ | Nghi ngờ +25 / −25 / về 0; gây dị giáo ngay; thần đối thủ +300 sức mạnh; chọn thần đối thủ và can thiệp để chạy ngay (bị Thiên nhãn báo trước nếu đang bật) |
| Đời sống tôn giáo | Bật Thiên nhãn 3 ngày; tạo lời cầu xin theo loại (bỏ qua điều kiện); cho mọi lời cầu xin hết hạn; ban thiên luật bỏ qua cooldown; kết thúc thiên luật (giữ / phạm); xoá cooldown thiên luật; biến hôm nay thành ngày thánh của lĩnh vực bất kỳ |
| Sứ giả & truyền giáo | Gọi Seraphim / Tiamat của bạn tại chỗ chuột; thả hiện thân thù địch tại chỗ chuột; hoàn thành mọi chuyến truyền giáo ngay; cải đạo settlement (công cụ bản đồ thế giới). Hiện thân của thần đối thủ cũng chạy được qua "Rival intervention now..." |
| Công cụ chọn pawn | Biến thành tín đồ (certainty 100%); phong Á thần bỏ qua giới hạn; Á thần +1 cấp / cấp tối đa; cho ban thánh khí lại; giáng Á thần về người thường; biến pawn thành champion của thần đối thủ |

Ép thần cấp được lưu vào save cho tới khi chọn lại "Automatic". Phép miễn phí không được lưu, khởi động lại game là tắt.

## Cấu trúc thư mục

```
About/                 About.xml
1.6/Assemblies/        ImAGod.dll (đã build sẵn)
Defs/                  thần cấp, phép lạ, bàn thờ, job, thought, hediff, main tab
Defs/Divinity/         con đường, 11 lĩnh vực (phép, cây, kỹ năng Á thần), Á thần
Defs/Messengers/       thân thể, chủng loài, kỹ năng và AI của sứ giả
Patches/               thêm truyền giáo vào settlement; patch Combat Extended
Languages/English/     chuỗi giao diện (Keyed)
Textures/ImAGod/       icon phép, icon tab, texture bàn thờ
Source/ImAGod/         mã nguồn C#
  Core/                GameComponent_Divinity (trạng thái), Def, settings, tiện ích
  Crisis/              nghi ngờ, dị giáo, thought khủng hoảng đức tin
  Domains/             con đường, lĩnh vực, cây phép, hộp thoại chọn
  Demigods/            Á thần, kỹ năng (CompAbilityEffect_DivinePower), Thăng hoa, thánh khí
  Petitions/           lời cầu xin
  Commandments/        thiên luật
  Sacrifice/           bệ tế máu
  Rivals/              thần đối thủ, can thiệp, champion
  Prophecy/            Thiên nhãn: giữ và báo trước sự kiện
  Powers/              DivinePowerWorker và các phép
  Prayer/              JobDriver_Pray, JoyGiver_Pray
  Patches/             Harmony: Storyteller.TryFire, IncidentQueue, nghi lễ, Pawn.Kill
  UI/                  MainTabWindow_Divinity
  Messengers/          sứ giả (Seraphim, Tiamat...), kỹ năng, AI, hiện thân thần đối thủ
  Missions/            truyền giáo, cải đạo settlement và faction
  Dev/                 công cụ Dev mode (debug actions)
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
- [x] Con đường Sáng tạo / Hỗn mang, 11 lĩnh vực, cây phép, Á thần
- [x] Công cụ Dev mode
- [x] Sứ giả thần thánh (Seraphim, Winged Beholder, Tiamat, Mušḫuššu) và chiến tranh hiện thân
- [x] Truyền giáo, cải đạo settlement và faction
- [x] Tương thích Combat Extended cho sứ giả
- [ ] Bản dịch tiếng Việt
- [ ] Ảnh preview cho Steam Workshop
