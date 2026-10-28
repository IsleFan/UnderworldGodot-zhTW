# 與原版（underworld-exhumed）功能對照與補齊紀錄

以 [abedegno/underworld-exhumed](https://github.com/abedegno/underworld-exhumed)（UW.EXE、UW2.EXE 逐位元組還原的原始碼與行為文件 `docs/behaviour/`）為基準，逐系統比對本專案的遊戲邏輯，記錄差異並逐項補齊。

- 做法：依原版的規則與數值重寫本專案的 C#，原版原始碼只用來確認細節，不整段搬移（反編譯碼源自原版程式，exhumed 的 MIT 授權不涵蓋那部分）。
- 驗證：能用原版對照資料（exhumed 的 `vectors/`、原版關卡與排程資料）的規則寫成單元測試（`tests/Underworld.Save.Tests`）；主線事件另以錄影測試掛鉤確認。
- 單元測試需要原版遊戲資料：環境變數 `UW_GOG_ROOT` 指向含 `UW1`、`UW2` 的資料夾（UW2 需有 `SAVE0`），未設定時用 `../UW2GOG`。
  - 準備方式（GOG 光碟映像沒有 `UW2/SAVE0`）：從 `/Applications/Ultima™  Underworld I.app/Contents/Resources/game/game.gog`（ISO 9660；2026-10-28 起 hdiutil 認不得，改用 `bsdtar -xf game.gog` 解出）取出 `UW`→`UW1`、`UW2`→`UW2` 並 `chmod -R u+w`；`UW2/SAVE0` 放 `DATA/LEV.ARK`、`DATA/SCD.ARK`、`DATA/PLAYER.DAT`，以及一份真的存檔的 `BGLOBALS.DAT`（`DATA/BABGLOBS.DAT` 格式不同不能用；用過 `build/release/UWGOG/UW2/SAVE1/BGLOBALS.DAT`）。
  - 執行：`UW_GOG_ROOT=<上述資料夾> env -u DOTNET_ROOT ~/.dotnet/dotnet test tests/Underworld.Save.Tests`（需 .NET 10 SDK，在 `~/.dotnet`）。基準：367 個、366 通過、1 略過（C5-2 其餘之後）。
- 原版原始碼：使用者本機已有 clone `~/Documents/Games/Underworld/underworld-exhumed`（`uw1/src`、`uw2/src`、`docs/behaviour/`）。
- 狀態：⬜ 待修　🔧 修正中　✅ 已修（附 commit）　🔍 待確認是否為問題　➖ 不處理（附原因）
- 遊戲：1 = UW1，2 = UW2，兩代 = 兩代都有。
- 「位置」是本專案修正前的程式位置（相對於 `src/`）。

## A. 會卡主線、弄丟物品或卡死

| 編號 | 遊戲 | 問題 | 位置 | 原版依據 | 狀態 |
|---|---|---|---|---|---|
| SCD-1 | 2 | 排程刪除一次性事件後索引 `si--` 沒有對應的 `si++`，位元組下溢成 255，該層之後的事件全部停止 | scd/scd.cs:186-190 | uw2 SCHEDULE.C FindSCDRowsToExecute | ✅ `66bc54e8` |
| SCD-2 | 2 | 排程進度沒有保留：每次呼叫重設「已修改」，沒執行到事件的區塊被丟掉並從原檔重讀 | scd/scd.cs:18、61-68 | uw2 SCHEDULE.C | ✅ `66bc54e8` |
| SCD-3 | 2 | 處理一般區塊時把時鐘 0 歸零 | scd/scd.cs:120 | uw2 SCHEDULE.C | ✅ `66bc54e8` |
| CLK-1 | 兩代 | `AdvanceTime` 是空函式：睡覺、修理、入獄、死亡不會跳過時間，也不跑排程 | player/playerdatclock.cs:86 | PLAYDATA／SKILLS | ✅ `66bc54e8` |
| CLK-2 | 2 | 每 20 分鐘的更新沒有更新「日期」、沒有跑排程（X 時鐘 0 每小時一步） | player/playerdatloop.cs:246-251 | player-upkeep.md、schedules.md | ✅ `66bc54e8` |
| CLK-3 | 2 | 對話 `x_clock` 設定時鐘 0 未實作 | conversation_functions/x_clock.cs:19-22 | conversations.md | ✅ `66bc54e8` |
| CLK-4 | 2 | 新遊戲時鐘從 0 開始（原版 15） | player/playerdatainit.cs:237 | uw2 CHARGEN.C init_char | ➖ 原版相同：`init_char` 設 15 後 `memset` 又全清為 0（schedules.md 的說法與程式不符） |
| MAG-1 | 2 | Altara 魔杖搜尋 y 軸從 `cX - range` 起算、`ValidTile` 參數對調，部分位置找不到力線標記 | magic/spellcasting_class_13.cs:160-162 | uw2 SPELLS2.C | ✅ `fc22a19c` |
| TRG-1 | 兩代 | 「執行一次後刪除」的機關分支沒有前進到下一個物件，觸發迴圈卡死 | triggers/trigger.cs:222-245 | uw2 OBJUSE.C／uw1 USEITEMS.C checkTrap、TRIGGER.C delete_trap | ✅ `96b465cb` |
| CNV-1 | 兩代 | 結束對話只放回「已選取」的玩家交易格物品，NPC 交給的物品會消失 | conversation/conversationvm.cs:644 等 | uw2/uw1 BARTER.C end_barter | ✅ `8ba43d99` |
| CNV-2 | 兩代 | `take_from_npc`／`take_id_from_npc` 失敗處理錯誤（物品消失或重複） | take_from_npc.cs、take_id_from_npc.cs | BARTER.C npc_barter_give／npc_barter_give_id | ✅ `8ba43d99` |
| CNV-3 | 兩代 | `find_barter_total` 存了物品種類而不是物件編號，接著的 `give_to_npc` 失敗 | find_barter_total.cs:29 | CONVERSE.C find_barter_total、conv_give_inv | ✅ `8ba43d99` |
| TRP-1 | 兩代 | 移除用過的機關（`delete_trap`／`kill_triggers`）用錯位置、觸發器仍連著已釋放的機關、計時觸發器沒移除、刪除物件機關只解除地圖格連結 | utility/ObjectRemover.cs:362、445、traps/a_delete_object_trap.cs:12 | TRIGGER.C delete_trap、kill_triggers、trigger_obj_del；EFFECT.C rem_timer_obj | ✅ `679fa690` |
| TRP-2 | 2 | 鬥技場逃跑機關檢查 `character == 0`，玩家是 1；競技場傳送區域檢查是 TODO | traps/a_hack_trap_coward.cs:10、utility/teleportation.cs:364 | TRIGGER.C | ✅ `4ea844f8` |
| TRP-3 | 2 | 近距離機關高度判斷錯誤（第 34、65、67、68 層） | traps/a_proximity_trap.cs:40-53 | TRIGGER.C | ✅ `94340b90` |
| UPK-1 | 2 | 劇情夢境旗標判斷顛倒、隨機夢少了機率判定；UW1 第一個 Garamon 夢走了隨機判定、隨機夢 4～9 不出現 | World/sleep.cs:505、560、571 | SKILLS.C dream（兩代） | ✅ `ae6b262f` |
| UPK-2 | 2 | Killorn 墜落：睡覺判斷顛倒且只限世界 2；醒著倒數提早兩次；進入時的排程補跑顛倒 | World/sleep.cs:103-105、player/playerdatloop.cs:165-166、World/specificworlds/killorn.cs:15 | SKILLS.C player_sleep、PLAYTIME.C duration_check、WORLDEV.C Killorn_just_crashed | ✅ `ae6b262f` |
| TRP-4 | 1 | 機關 0x32 未實作（第 7 層 NPC 主動走來對話） | traps/hack_trap.cs | TRIGGER.C | ✅ `94340b90` |
| TRP-5 | 1 | 設定變數機關 z=0 時寫錯（第 3 層應設任務旗標 7） | traps/a_set_variable_trap.cs:49 | TRIGGER.C | ✅ `4ea844f8` |
| TRP-6 | 1 | NPC 踩到傳送觸發器時傳送玩家 | traps/a_teleport_trap.cs:22 | TRIGGER.C | ✅ `4ea844f8` |
| SCD-4 | 2 | 排程事件 4（設定任務旗標）只印訊息 | scd/functions/scd_changequest.cs:7 | uw2 SCDEVENT.C ev_set_qbit | ✅ `ef4f708c` |
| TRP-7 | 2 | 機關 44（睡眠，第 12 層）、37（排程追上進度）未實作 | traps/hack_trap.cs | TRIGGER.C | ✅ `94340b90` |
| TRP-8 | 兩代 | 未實作的機關回傳 false 會中止整串觸發（原版一律繼續） | traps/trap.cs:300-306 | TRIGGER.C | ✅ `4ea844f8` |

## B. 規則與原版不同

### 戰鬥（CMB）
| 編號 | 遊戲 | 問題 | 位置 | 狀態 |
|---|---|---|---|---|
| CMB-1 | 兩代 | 砍、劈、刺在輸入、傷害、揮擊高度三處的編號不一致 | interaction/combat/combat.cs:12-27、combat_globals.cs:26-41 | ✅ `936eb23b` |
| CMB-2 | 兩代 | 生物選擇攻擊招式的判斷顛倒（常用 0 傷害的第三招） | npc/npcai.cs:2997 | ✅ `936eb23b` |
| CMB-3 | 兩代 | 擊殺經驗少乘 4、強力生物加成用加的、非玩家擊殺也給經驗 | player/playerdat.cs:768-772、interaction/damage.cs:170 | ✅ `936eb23b` |
| CMB-4 | 兩代 | 裝備不會損耗（打門、被爆擊、嚴重失手） | combat.cs:452、467、488 | ✅ `936eb23b` |
| CMB-5 | 兩代 | 盾牌不加防禦、護甲不看品質、玩家防禦少了一半武器技能 | player/playerdatloop.cs:642-669、critterobjectdat.cs:357 | ✅ `936eb23b` |
| CMB-6 | 兩代 | 盟友不加入戰鬥；追擊的放棄、感知、到達判斷錯誤；守衛放棄距離；碰撞處理顛倒；繞行計算錯亂 | npc/npcai.cs 607、1128-1200、1580、2547、2804 | ✅ `fb8cc7bd`、`edee329a` |
| CMB-7 | 兩代 | 毒武器只加 0～1 點；生物下毒用 UW2 的護甲判定 | combat.cs:224、422 | ✅ `936eb23b` |
| CMB-8 | 兩代 | 命中部位判斷、飛彈高度 | combat.cs:954、combat_missile.cs:100 | ✅ `936eb23b` |
| CMB-9 | 兩代 | 小差異：弱點加倍上限、Lifestealer、開門特殊武器、is_sharp | damage.cs:615 等 | ✅ `936eb23b` |

### 魔法（MAG）
| 編號 | 遊戲 | 問題 | 位置 | 狀態 |
|---|---|---|---|---|
| MAG-2 | 兩代 | 抗性法術與附魔（防火、防飛彈、抗毒、魔法防護）沒有效果：`PlayerDamageTypeScale` 沒人讀 | magic/spellcasting_class_0123.cs:71、interaction/damage.cs:506、543 | ✅ `936eb23b` |
| MAG-3 | 兩代 | 加速沒有效果；UW2 勇氣判斷值寫錯；左手戒指與手部欄位附魔被忽略 | spellcasting_class_0123.cs:76、player/playerdatloop.cs:545、587-602 | ✅ `5d85b863` |
| MAG-4 | 兩代 | 驅除不死生物：UW2 打任何生物、浮空骷髏不消滅；UW1 無效 | spellcasting_class_7.cs:430-464、spellcasting_class_6.cs:283 | ✅ `5d85b863` |
| MAG-5 | 兩代 | 持續型法術時間太短、提早一輪結束 | spellcasting_activeeffects.cs:20-23、playerdatloop.cs:109-110 | ✅ `5d85b863` |
| MAG-6 | 兩代 | 範圍法術選格錯誤、範圍太小、一律以玩家為中心 | spellcasting_class_6.cs:65-85、utility/CallBacks.cs:155 | ✅ `5d85b863` |
| MAG-7 | 1 | 五個生物類法術變成要選目標（原版直接作用在前方） | spellcasting.cs:75-80 | ✅ `135bc0c3` |
| MAG-8 | 兩代 | 施法判定：UW1 用 UW2 公式、Britannia 擋第 3 圈、UW2 無效仍扣魔、物品施法沒冷卻、麻痺仍可施法 | runicmagic.cs:339、476、spellcasting.cs:108 | ✅ `135bc0c3` |
| MAG-9 | 2 | 附魔難度與等級、地圖區域範圍 | enchanting.cs:88、138、145、spellcasting_class_13.cs:71 | ✅ `135bc0c3` |
| MAG-10 | 兩代 | 魔杖、卷軸、藥水以物品本身當施法者；治療在生物或機關施放時治療玩家；0～3 類由生物施放時套在玩家身上 | MagicEnchantment.cs:280、spellcasting_class_4.cs、spellcasting_class_0123.cs:31 | ✅ `5d85b863` |
| MAG-11 | 1 | 召喚怪物太弱、偵測怪物範圍 8（原版 10） | spellcasting_class_8.cs:84 | ✅ `135bc0c3` |
| MAG-12 | 兩代 | class 12 防護／強韌附魔只算頭盔與胸甲、少 +1、強韌加錯地方 | spellcasting_objects.cs:57-75 | ✅ `5d85b863` |

### 對話與交易（CNV）
| 編號 | 遊戲 | 問題 | 位置 | 狀態 |
|---|---|---|---|---|
| CNV-4 | 兩代 | 9 個內建函式未綁定（plural、append、copy、find、val、add_to_npc_inv、end_barter、say、respond）；STRCMP 指令沒有；RESPOND_OP 不出棧也不輸出 | imported_functions.cs:278、conversationvm.cs:555 | ✅ `ecd6df20` |
| CNV-5 | 兩代 | 虛擬機：OR、AND 用位元運算；除以零弄亂堆疊；取餘數除以零丟例外；未知指令被略過 | conversationvm.cs:104、139、148 | ✅ `ecd6df20` |
| CNV-6 | 兩代 | 交易價值與脾氣（數量、估價誤差、最低值、貪婪、耐心、種子）、成交處理、babl_hack 8、UW1 索取規則 | conversationtrade.cs:237-248、conversationinitialisation.cs:186-191、babl_hack.cs:94、do_demand.cs:38、43 | ✅ `b12f3770` |
| CNV-7 | 兩代 | `give_ptr_npc`、`x_obj_pos` 壞掉；匯出 `npc_hunger` 顛倒；`remove_talker`／`transform_talker` 用錯地圖格 | give_ptr_npc.cs、x_obj_pos.cs、conversationvariables.cs:250、remove_talker.cs:12 | ✅ 先前各項、`94977084`（背包給物）；remove/transform_talker 查證後未能確認有誤 |
| CNV-8 | 兩代 | 其他內建：x_traps、give_to_npc、set_inv_quality、place_object、teleport_talker、switch_pic、NPC 物品欄生成、UW2 關閉時 8 步動畫 | 多處 | ✅ `4639bf16`、`94977084`；對話後 8 步動畫補跑未做（本專案動畫即時播放） |
| CNV-9 | 兩代 | 字串：contains 比對子字串而不是整字；compare 沒做 @ 替換；替換少了 C 類、@@、P 類索引 | conversationio.cs | ✅ `ecd6df20`、`e870f97e` |

### 玩家狀態（UPK）
| 編號 | 遊戲 | 問題 | 位置 | 狀態 |
|---|---|---|---|---|
| UPK-3 | 兩代 | 死亡反而增加經驗 | player/playerdat.cs 約 727、playerdatdeath.cs:57、179、195 | ✅ `a9a60d7a` |
| UPK-4 | 兩代 | 經驗：UW1 獲得減半、少了 >96000 規則、上限處理、升級訊息 | player/playerdat.cs:745 | ✅ `a9a60d7a` |
| UPK-5 | 兩代 | 清醒回血太強、慢速週期（UW1 應 24）、飢餓遞減 | playerdatloop.cs:218-244 | ✅ `a9a60d7a` |
| UPK-6 | 兩代 | 睡覺的飢餓、酒醉、回血、疲勞、光源燃燒 | World/sleep.cs:117、163、191-232 | ✅ `a9a60d7a` |
| UPK-7 | 兩代 | 光源燃燒速度用錯資料、熄滅門檻、時間停止時仍燃燒、游標上的光源 | playerdatloop.cs:378 | ✅ `a9a60d7a` |
| UPK-8 | 兩代 | 技能提升比較屬性編號而非數值、UW2 用 UW1 的表、沒有 30 上限 | player/playerdatskills.cs:530-541 | ✅ `a9a60d7a` |
| UPK-9 | 兩代 | 負重：UW1 用 UW2 公式、重複計算、容器內容 | player/playerdat.cs:796、playerdatinventory.cs:834 | ✅ `a9a60d7a` |
| UPK-10 | 兩代 | 起始食物值、UW1 疲勞、食物回血（UW1 /8）、酒醉上限、飲料回血超過上限 | playerdatainit.cs:166、playerdatstatus.cs:377、676、objects/food.cs:219 | ✅ `a9a60d7a` |
| UPK-11 | 兩代 | 生物 10 分鐘檢查（yearly_checkup）、睡覺時物件清除與平靜、UW1 第 9 層不能睡、Tybal 寶珠魔力規則、學院無魔層、游泳計數上限 | playerdatloop.cs:233、291、344、World/sleep.cs:91、153、playerdat.cs:789 | ✅ 見修正紀錄（睡覺時的物件清除另見「UPK-11 其餘」） |

### NPC AI 與排程（AI）
| 編號 | 遊戲 | 問題 | 位置 | 狀態 |
|---|---|---|---|---|
| AI-1 | 兩代 | 流浪怪物只在玩家附近出現（原版只在遠處） | traps/a_create_object_trap.cs:236 | ✅ `d0a37aa2` |
| AI-2 | 兩代 | 遊蕩：UW2 站立轉走動機率顛倒、碰撞只會轉 -90°、飛行生物高度 | npc/npcai.cs:2219-2320 | ✅ `baf54057` |
| AI-3 | 1 | UW1 特有規則被 UW2 取代：敵對對話者轉守衛、非敵對生物開門打門、聽到聲音規則、路徑步數 32、第 6 層對話 0x16 | npc/npcai.cs:1409、1814、2148、pathfind.cs:293 | ✅ 路徑步數（先前）、`fb8cc7bd` |
| AI-4 | 1 | UW1 目標 3（跟隨）是空的 | npc/npcai.cs:770 | ✅ `d0a37aa2` |
| AI-5 | 2 | UW2 路徑搜尋範圍在世界 0 為 10 格（原版 5） | pathfind.cs:108-113 | ✅ `d0a37aa2` |
| AI-6 | 2 | 排程事件 1 與對話匯出改目標沒經過 `critter_set_goal` | scd/functions/scd_goalgtarg.cs:29、conversationvariables.cs:262 | ✅ `d0a37aa2` |
| AI-7 | 2 | 城堡作息（12 小時制、第 3/5/9 階段、Miranda、位置 5、隨機格、對話 0x81） | traps/a_hack_trap_castleschedule.cs | ✅ `2bfc04e7` |
| AI-8 | 兩代 | 移動生物沒有佔用檢查、飛行生物放在地上、固定 3,3 | npc/npcutil.cs:132 | ✅ `baf54057`、`94977084` |
| AI-9 | 兩代 | 層級經過時間的回血公式、聽到聲音位元；關門機率；鬥技場屍體；偷竊訊息；睡覺伏擊 | npc/npc.cs:543-549、scd_closedoors.cs:38、npc/npcdeath.cs:562、interaction/thief.cs:119 | ✅ `fd770644` |
| AI-10 | 2 | 損壞擁有者的物品不會激怒擁有者種族 | interaction/damage.cs:256-259 | ✅ `d0a37aa2` |
| AI-11 | 2 | 第 1 層抵達點的門沒有打開 | utility/teleportation.cs:467-470 | ✅ `d0a37aa2`（原列為 UW1，實為 UW2） |

### 機關觸發（TRP）
| 編號 | 遊戲 | 問題 | 位置 | 狀態 |
|---|---|---|---|---|
| TRP-9 | 1 | 門機關不換鎖（27 個中 24 個受影響）；會移動的門未實作 | traps/a_door_trap.cs:36、45、71 | ✅ `e4b70fd8` |
| TRP-10 | 兩代 | 傳送少了「迷路」位元與找空格邏輯（含 Talorus Escher 房間） | utility/teleportation.cs | ✅ 迷路位元（先前）、`94977084` |
| TRP-11 | 兩代 | 技能機關難度少乘 3 | traps/a_skill_trap.cs:29 | ✅ `d96597dd` |
| TRP-12 | 兩代 | 機關 62（link 1）改了玩家的目標 | traps/a_hack_trap_changegoal.cs:24 | ✅ `d96597dd` |
| TRP-13 | 2 | Qbert 月門重試會累減樓層；謎題變數應為字組 | traps/a_hack_trap_qbert.cs:378 | ✅ `e4b70fd8` |
| TRP-14 | 1 | UW1 觸發器套用 UW2 的物品規則，丟出的物品不觸發 | triggers/trigger.cs:83 | ✅ `d96597dd` |
| TRP-15 | 兩代 | 產生物件機關：UW1 機率判定兩次、UW2 隨機怪物範圍與基準、附近生物判斷中心、範本內容共用、動畫清單 | traps/a_create_object_trap.cs | ✅ `e4b70fd8` |
| TRP-16 | 兩代 | 計時觸發器的距離、Roaming Sight、物品 0x1B9、計數迴繞 | timers.cs:61-71 | ✅ `d96597dd` |
| TRP-17 | 兩代 | 跳躍機關沒看速度門檻 | traps/a_jump_trap.cs:20-38 | ✅ `d96597dd` |
| TRP-18 | 兩代 | 機關 39 中止整串、ward／tell 機關繼續、傷害機關少了抗性與毒處理 | traps/hack_trap.cs:347 等 | ✅ `d96597dd` |
| TRP-19 | 兩代 | 解除機關的難度、結果、可偵測種類、失敗處理 | 多處 | ✅ `e4b70fd8` |
| TRP-20 | 兩代 | 坑洞機關高度 15 封死格子；UW1 改地形限制；UW2 物品欄機關失敗分支 | traps/a_pit_trap.cs:25、tileinfo.cs:552、590 | ✅ `e4b70fd8` |

## C. 尚未比對的系統

| 編號 | 系統 | 狀態 |
|---|---|---|
| TODO-1 | 玩家移動與物理、碰撞、游泳、跳躍、坡道 | ✅ 5 個檔案都比對並修正（修正紀錄 C1-1～C1-5）；各節列有未做項目 |
| TODO-2 | 物件使用（`use`）、撿取規則、裝備欄規則、容器 | ✅ 比對並修正主要項目（修正紀錄 C2-1～C2-5）；各節列有未做項目 |
| TODO-3 | 介面：物品欄、人物欄、符文袋、地圖、選項選單 | ✅ 比對並修正主要項目（修正紀錄 C3-1～C3-6）；各節列有未做項目 |
| TODO-4 | 過場動畫（小視窗過場、調色盤複製、計時回呼）與 UW2 守護者過場文字 | ✅ 比對並修正腳本規則（修正紀錄 C4-1）；播放引擎本身與原版結構不同，列在未做 |
| TODO-5 | 存讀檔（含 UW2 自動地圖探索紀錄）、換層與層級持續 | ✅ 比對並修正主要項目（修正紀錄 C5-1、C5-2）；各節列有未做項目 |
| TODO-6 | 配樂主題切換、音效 | ✅ 比對並修正主要項目（修正紀錄 C6-1～C6-3）；各節列有未做項目 |

## 剩餘項目總表

2026-10-28 清點：逐一檢查原版 `.C` 檔是否比對過，再整理各節修正紀錄的「未做」，去掉後來批次已修好的。細節看括號裡的修正紀錄。

### 一、從沒完整比對過的原版模組（遊戲邏輯）

| 原版檔案 | 內容 | 目前狀況 |
|---|---|---|

不列入：3D 繪製、圖形、ARC／LZSS 與檔案格式、系統、搖桿，不屬於遊戲規則。

### 二、已比對過、但還沒做的項目

會影響遊戲的：

- 換層（C5-1）：（找不到落腳處時原版不放置玩家：本專案的引擎需要玩家有位置，玩家照樣死亡、仍放在目標格，列為刻意差異；UW2 load_digi_fx 是 DOS 換層時重新載入數位音效，本專案隨用隨載，不適用。）
- 音效（C6-2、C6-3）：update_digi_playback、數位音效的快取與循環已完成，要遊戲內聽過確認。
- 對話：UW2 對話結束的 8 步動畫補跑（CNV-8）：本專案的動畫即時播放，沒有逐格動畫清單，不適用。（`teleport_talker` 延到對話結束、CNV-9 的 C 類／@@／P 類替換，2026-10-28 確認原本就已照原版。）

只影響 DOS 讀本專案存檔的（C5-2）：都已完成。（動畫結尾、計時器清零、區塊旗標已在「C5-2 其餘」完成。）

刻意保留的差異（2026-10-28 使用者確認保留）：過場一律顯示字幕、有語音的台詞等前一段說完（中文化需要字幕，且字幕要對得上語音）；UW1 刪光筆記不存檔的 bug 不照做；換層找不到落腳處仍放置玩家、地震掉落物直接生成移動物件（引擎需要玩家與物件有位置）。CLK-4（新遊戲時鐘）查證後與原版相同，不是差異。拖曳物品：原版 UW1 左右鍵都能拖、UW2 只能右鍵（INVPANEL.C 的 `inplist->cmd != 1`）；使用者要求兩代統一預設右鍵，啟動畫面可改成左鍵（`uwsettings.dragwithleft`、`Controls.DragButton`，背包與 3D 畫面「看」之後的拖曳都適用）。

已照原版改回：夢境中拿著的物件（原版進夢境時照常帶著；醒來時 punt_void 用 punt_mouse_obj 把它放在玩家附近，留在夢境；本專案原本在夢境中每次傳送都丟在腳下，醒來時反而帶出去，見 C5-1）、主選單重播主題曲（原版本來就會重播，見 C6-1）。

### 三、遊戲內驗證

2026-10-28 用 `--write-movie` 實機截圖確認（需要 `DOTNET_ROOT=~/.dotnet`，並直接指定 `res://scenes/Underworld.tscn`）：
- 地圖（UW1，`UW_AUTOSTART=1 UW_TESTMAP=1`）：格子照原版位置與加暗；玩家標記是原版圖示（截圖時發現標記沒畫，原因是 GR 的 type 8 圖沒解碼，已修）。新筆記「TEST」的左緣在點擊處、頂端在點擊處上方 4 列，字高 4 列，和原版公式一致。
- 小視窗過場（UW1 死亡骷髏 0x102，`UW_AUTOSTART=1 UW_TESTCUTS_INGAME=258`）：畫面在 3D 視窗框內，沒有溢出，也沒有被切掉。
- 全螢幕過場：UW2 CS030 的字幕列（畫面在第 160 列裁切）；UW1、UW2 字幕靠底（截圖時發現字幕沒有靠底，已修）。
- 傳送淡出淡入（`UW_AUTOSTART=1 UW_TESTTELEPORT=1`）：截圖時發現完全沒有淡出淡入。原因是 3D 視窗的 shader（viewport_combination）直接寫入 COLOR，Modulate 不起作用；改成在 shader 加 `brightness` 參數。重錄後量得 3D 視窗平均亮度：先用 12 格從 36.5 降到 11.4（約 31%，LIGHT.DAT 算出的 UW1 第 12 列是 33%），接著一格全黑，再用 12 格亮回來，與原版的 out3d／in3d 一致。
- 量測確認（除錯掛鉤 `UW_TESTSELFCHECK=1`，印出 SELFCHECK 行）：
  - 物理時鐘：3 秒內前進 768 tick，正好每秒 256。
  - 數位音效左右：西邊來的聲音 pan 0x7F（Miles 慣例，極左），玩家轉 180 度後是 0x01（極右），音量 0x37 不變。
  - 夢境物件：醒來後手上是空的，物件放在玩家原本所在的格子。
  - 配樂：遊戲中第 4 首播完，換成漫遊曲（第 10 首）並在播放；主選單第 1 首播完，重新播第 1 首（`UW_TESTMENU=1`）。
- 量不到的只有主觀感受：聲音實際聽起來如何、移動手感。2026-10-28 使用者決定之後自己試玩確認，比對工作到此結束。

A、B、C 三類的修正都只通過建置與單元測試，全部還沒在遊戲內錄影確認。

建議順序：第二部分各項。

## 修正紀錄

（每修完一個系統在這裡記錄 commit 與驗證方式）

### UW2 排程與時鐘（SCD-1～3、CLK-1～3；CLK-4 不處理）
- `scd/scd.cs`：依原版 SCHEDULE.C 改寫 `ProcessBlock0`（Sched_WrapTime）、`ProcessGeneralBlock`（Sched_SetTime，拿掉把 X 時鐘 0 歸零）、`SetSomeValuesInSCDRows_ovr151_D3`（時鐘倒退不執行時把下一筆往回移，原本是空函式）、`FindSCDRowsToExecute`（刪除事件回傳 4 時 `i--` 再 `i++`，等於停在同一格）；事件數、各層時間與下一筆、事件時間全部改用 16 位元（原本只讀低位元組，時間 65535 的「永不執行」事件會在時鐘到 255 時誤執行）；區塊不再在沒修改時丟棄（原本會從存檔資料夾重讀，失去先前的進度），只用來決定丟棄的 `ArkHasBeenModified` 一併移除。新增 `AdvanceTimeOfDay()`：X 時鐘 0 加 1（mod 72）後只跑區塊 0（PLAYTIME.C duration_check 每 60 次）。
- `scd/scd_util.cs`：`DeleteRow` 改用 16 位元欄位。`savegame/ScdArkWriter.cs`：開頭註解更新（區塊載入後保留到下次讀檔）。
- `player/playerdatclock.cs`：`AdvanceTime(秒)` 實作 pass_time：`ClockValue += 秒<<8`；UW2 另設 X 時鐘 0 = ClockValue/0x4B000 mod 72，並 `scd.ProcessSCDArk(1)`。
- `player/playerdatloop.cs`：每 60 次（20 分鐘）的 TODO 改呼叫 `scd.AdvanceTimeOfDay()`。
- `conversation_functions/x_clock.cs`：設定時鐘 0 時，遊戲時鐘移動（新值−舊值）×0x4B000（BABLHACK.C x_clock）。
- CLK-4 查證：原版 `init_char`（CHARGEN.C）先設 `xclock[0] = 0xF`，後面 `memset(player->xclock, 0, 0x10)` 又清掉，新遊戲實際是 0，與本專案相同，不改。原版 `pass_time` 會重設 `lastDurCheck`，睡覺後不補跑計時；本專案的計時以實際秒數計，同樣不會補跑。
- 單元測試 `tests/Underworld.Save.Tests/ScdScheduleTests.cs`（新）：自製區塊、事件全用「變數運算」（只改遊戲變數，不碰世界與引擎）。一次性事件刪除後之後的事件繼續執行；時間 65535 的事件在時鐘 255 不執行；連跑兩次不重複執行、不把時鐘 0 歸零；時鐘 0 跑每日排程並在 71→0 換日（原版行為：換日當下時間 0 的事件不執行，到 1 才執行）。前 3 項在修正前的程式上確認會失敗。`TestData.cs` 可用 `UW_GOG_ROOT` 指定遊戲資料。
- 已確認：建置 0 錯誤；單元測試 284 通過、1 略過。
- 未做：遊戲內錄影驗證（需要一個停在特定排程前的 UW2 存檔，並等待遊戲時間或睡覺）。
### SCD-4：排程事件 4 設定任務旗標
- `scd/functions/scd_changequest.cs`：依 SCDEVENT.C `ev_set_qbit`，把任務旗標 row[5] 設成 row[6]（`playerdat.SetQuest`）。原版 SCD.ARK 只有一筆事件 4（區塊 1 第 43 筆，第 5 層、時間 14、一次性、任務 56 設 1）；原版把值不遮罩地加在該位元，值只有 0／1 時與 `SetQuest` 相同。
- 單元測試 `ScdScheduleTests.Event4_SetsTheQuestFlag`：照原版那一筆做，確認任務 56 被設定、同一組的任務 57 不受影響、一次性事件被刪除。單元測試 285 通過、1 略過。

### MAG-1：Altara 魔杖（切斷力線）
- `magic/spellcasting_class_13.cs` `AltarasWand`：依 SPELLS2.C `thump_your_magic_twanger_froggie`／`check_Guardian_magic_marker` 與 SPELLS.C `process_area`。y 軸原本從 `cX - range` 起算、`ValidTile` 參數對調，改為 `cY` 與 `(aX, aY)`；搜尋範圍改為原版的玩家位置 −2～+3（`process_area` 是 w+1 格，w = 5）；這一界的力線已切斷時，原版仍會移除標記並結束搜尋、顯示沒有效果，原本是留著標記繼續找。力線位元、全部切斷設任務 14、冰洞設任務 52、特效與音效原本就與原版相同。
- 驗證：建置 0 錯誤。這段會呼叫動畫、音效、訊息框（Godot），沒有單元測試；遊戲內未錄影（需要在力線標記旁的存檔）。

### TRG-1：使用物件時觸發所含的機關（`trigger.TriggerObjectLink`）
- `triggers/trigger.cs`：依 checkTrap。沒有旗標的普通機關在「使用」時執行後，原本只印 TODO、`ObjectToTrigger` 不變，迴圈卡死；改為用 `ObjectRemover_OLD.RemoveTrapChain` 從物件內容刪掉機關鏈（delete_trap）並結束。背包物件的資料不在關卡資料區，這時只結束、不刪除（原版背包與關卡共用物件記憶體，這點與原版不同）。
- 同一處另修兩項：UW2 開關在第二段狀態（index > 7）時要跑第二個機關，原本從物件內容開頭再找一次、找到的還是第一個，改為從第一個機關的下一個開始找；UW1 的 checkTrap 只觸發物件裡的第一個機關／觸發器，也沒有「門機關不在使用時觸發」的例外，原本兩代都照 UW2 的規則。
- 驗證：建置 0 錯誤、單元測試 285 通過 1 略過（沒有針對這段的單元測試：執行機關會碰到世界與引擎）。遊戲內未錄影。

### CNV-1～3：交易格物品（兩代）
- 本專案的交易模型與原版不同：原版把 NPC 的物品從身上移到交易格，成交後玩家要自己從 NPC 交易格拿走；本專案的 NPC 物品一直留在 NPC 身上，成交時 `SwapTradedObjects` 直接放進玩家背包（放不下就放腳邊）。這個模型能正常運作，不改；以下只修會讓物品消失或重複的地方。
- CNV-1 `conversation/conversationvm.cs`：對話結束時玩家交易格的物品全部放回玩家腳邊（原本只放「已選取」的，沒選的和 NPC 交給玩家而放在交易格的物品會消失；原版 end_barter 全部放回）。
- CNV-2 `take_from_npc.cs`、`take_id_from_npc.cs`：依 npc_barter_give／npc_barter_give_id（兩代相同）。手上有東西時回傳 0、不給（原本 `take_from_npc` 會蓋掉手上的物品，`take_id_from_npc` 會不移出 NPC 身上就放進交易格，造成重複）；`take_id_from_npc` 只給 NPC 身上（最上層）的那個物件，否則回傳 0；拿不動時先放空的玩家交易格（不選取），滿了才放到 NPC 腳邊、回傳 2（原本 `take_from_npc` 交易格滿時物品已移出 NPC 卻沒放到任何地方，`take_id_from_npc` 放在玩家腳邊）。共用 `HandOverFromNPC`。因為模型不同，兩者都略過正放在 NPC 交易格的物品（原版這些物品已不在 NPC 身上，所以找不到）。
- CNV-3 `find_barter_total.cs`：輸出陣列存物件編號（原本存物品種類）。`give_to_npc.cs` 依 conv_give_inv：列出的物件必須全部是玩家已選取的交易物品才給，否則回傳 0、一個都不給（原本給一部分）。金幣併入 NPC 原有金幣堆（npc_inv_add）沒有做，歸在 CNV-6。
- 驗證：建置 0 錯誤、單元測試 285 通過 1 略過（交易格是 Godot 介面，沒有單元測試）。遊戲內未錄影。

### TRP-2、5、6、8：機關小項
- TRP-2 `traps/a_hack_trap_coward.cs`：依 do_trap_hack 30，只在競技場打鬥中作用；觸發者是玩家（物件 1，原本誤判 0）算玩家逃跑，否則算對手逃跑；之後一律繼續機關鏈（原本對手時回傳 false 中止）。`pitsofcarnage.DefeatLivingPitFighter` 依 arena_opponent_runs 先把勝場（任務 129）加 1 再更新最佳紀錄（X 時鐘 14）。`utility/teleportation.cs`：打鬥中傳送時依 do_teleport 判斷逃跑：換樓層、目的地不在競技場內（新增 `InArena`），或換到另一個競技場（新增 `Mpos`，原版 mpos）；原本目的樓層 0（同一層）也被當成換樓層。
- TRP-5 `traps/a_set_variable_trap.cs`：UW1 z=0 時運算元就是任務旗標編號（原本當成 `1 << 運算元`）。新增單元測試 `SetVariableTrapTests`（修正前失敗）。
- TRP-6 `traps/a_teleport_trap.cs`、`trap.cs`：傳送機關只傳送玩家（兩代 do_teleport：NPC 不會移動、也不能換層），原本 NPC 觸發時會傳送玩家。
- TRP-8 `traps/trap.cs`：不論機關有沒有實作都接著執行機關鏈（UseTrap 只有特定機關中止，本專案以 `triggerNextIndex = 0` 表示），原本未實作或回傳 false 的駭客機關（例如入侵機關 5）會中止整串。
- 驗證：建置 0 錯誤、單元測試 286 通過 1 略過。遊戲內未錄影。

### TRP-3、4、7：近距離機關與缺少的駭客機關
- TRP-3 `traps/a_proximity_trap.cs`：高度條件依 UseTrap TRAP_PROXIMITY：fine x 為 0 時物件高度要 ≤ 機關高度，fine y 為 0 時要 > 機關高度（原本兩個條件都寫反）。
- TRP-4 新增 `traps/a_hack_trap_talktoplayer.cs`（UW1 駭客機關 0x32，第 7 層）：第一個對話編號 0xD8 的活動中 NPC，若目標是 7（站著）先改成 1（回家），然後和玩家對話（gronk_whoami、gronkify_talkto_player）。
- TRP-7 `traps/hack_trap.cs`：UW2 駭客機關 37 補跑全部排程（`scd.ProcessSCDArk(1)`，Sched_SetAllClocks）；44 以機關 owner 作為睡法睡覺（`sleep.Sleep`，player_sleep），第 12 層。
- 驗證：建置 0 錯誤、單元測試 286 通過 1 略過（這些機關會碰到地圖、NPC 與對話，沒有單元測試）。遊戲內未錄影。

### TRP-1：移除用過的機關
- `utility/ObjectRemover.cs`（`ObjectRemover_OLD`，機關系統用的那套）：
  - `RemoveTrapChain` 搜尋整張地圖時傳的是地圖格第一個物件的編號，不是串列位置，改為 `tile.Ptr + 2`。原本找不到指向機關的觸發器，用過的一次性觸發器仍連著已釋放的機關。
  - `RemoveTriggersPointingAtTrap` 依 kill_triggers：刪掉一個觸發器後，串列位置不往前移，因為下一個物件已經補到同一個連結。原本會改到已釋放物件的 next 欄位。另外每刪一個就把機關的觸發器計數減 1，讓整張地圖的搜尋能提早結束。
  - `RemoveObjectAndChainFromLists` 清內容物時傳的是 link 的值，改為物件的 link 欄位位置 `PTR + 6`（Obj_FreeLinkChain）。這個函式改成 public，給刪除物件機關使用。
  - UW2 刪除計時觸發器時，從計時清單移除（`RemoveTriggersPointingAtTrap`、`RemoveTriggerChain`）。
- `World/timers.cs`：新增 `RemoveTimerTrigger`，依 rem_timer_obj，用最後一筆補上被刪的位置。
- `traps/a_delete_object_trap.cs`：依 TRAP_DELETE_OBJECT，把那一格上的連結物件連同內容物一起釋放。原本只從地圖格解除連結並釋放物件本身，內容物沒有處理。UW2 的連結物件是機關或觸發器時改用機關的刪法（trap_obj_del），動畫物件會一併移除動畫。
- 另外發現（未修）：一般物件刪除用的另一套 `ObjectRemover.RemoveObject_experimental` 只在停止移動中的物件時用到。它的 `RemoveTriggerChain` 判斷與原版 trigger_obj_del 相反（計數 1 時應該刪整個機關），地圖格座標也對調（`Tiles[owner, quality]`）。兩套刪除程式並存，之後應該統一成一套。
- 驗證：建置 0 錯誤、單元測試 286 通過、1 略過。這部分會碰到地圖物件串列，沒有單元測試；遊戲內也還沒錄影。

### UPK-1～2：夢境與 Killorn 墜落
- UPK-1 `World/sleep.cs`：
  - UW2 劇情夢 0～3：原本旗標已設才算「未看過」，判斷相反，改為旗標未設才算，劇情夢才會出現。
  - UW2 沒有劇情夢時，補上 1/(4+4×舒適度) 的機率判定才出隨機夢 4～6；原本每次都出。
  - UW1：隨機夢的判斷寫成 `== 0`，應為 `< 0`。原本第一個 Garamon 夢（編號 0）要先過機率判定，而沒有劇情夢時（−1）隨機夢 4～9 從不出現。
- UPK-2：
  - `World/sleep.cs`：睡覺時任務 50 已設、54 未設（墜落將至且尚未發生）就墜落並致死，不限世界 2；原本條件寫反（54 ≠ 0），而且只限 Killorn。
  - `player/playerdatloop.cs`：醒著的倒數（任務 134）依原版 `quest_bytes[6]-- == 0`，先比較舊值再遞減；原本比較「新值 − 1」，早兩次結束。
  - `World/specificworlds/killorn.cs`：墜落後補跑排程只在不是換關卡時進行；原本寫反。換關卡時呼叫的條件（離開 Killorn 第 1 層、任務 50）與原版相同，不改。
- 驗證：建置 0 錯誤、單元測試 286 通過、1 略過。夢境、睡眠和 Killorn 都會碰到過場動畫與世界狀態，沒有單元測試；遊戲內也還沒錄影。

### 戰鬥（CMB-1～5、7～9）、抗性（MAG-2）與亂數
- 亂數 `utility/rng.cs`：`Rng.r.Next` 改為原版的 Turbo C `rand()`（`seed = seed * 0x015A4E35 + 1`，取第 16～30 位元），原本是 .NET 亂數、`Rng.Seed` 設了也沒有作用（對話初始化會以 NPC 設種子）。新增 `NextScaled`（`rand() * max / 0x8000`），`DiceRoll` 依 rollem 改用它。
- 原版對照資料：從 exhumed 複製 `uw2/vectors` 的 skill_check、rollem、pickloc、is_sharp、check_res 到 `tests/Underworld.Save.Tests/vectors/`（附出處與授權說明），新增 `Uw2VectorTests` 逐筆比對結果與亂數狀態，全部吻合。
- CMB-1 `combat.cs`、`combat_globals.cs`：揮擊種類以輸入與武器動畫的編號為準（0 砍、1 劈、2 刺），修正傷害取值與揮擊高度兩處原本對不上的編號（原本刺擊用劈擊傷害等）。
- CMB-2 `npc/npcai.cs` `ChooseMeleeAttackToMake`：依 crit_attack，選招迴圈條件改為 `機率 <= r`（原本相反，常選到第三招）、出招距離 0x64（原本 0x100）、側移轉 ±90°（原本 ±45°）；UW1 用 UW1 的動作序列（戰鬥姿勢 0、後退 7、走路 0x2C，原本用 UW2 的 1、2，在 UW1 會顯示成攻擊動畫），影格在結尾遞增。
- CMB-3 `playerdat.AwardXPKill`、`damage.cs`：擊殺經驗 `4 × 經驗值 + 2d經驗值`，強力生物乘以 (24～47)/16（原本少乘 4、強力加成用加的）；只有玩家擊殺才給（原本任何來源都給）。
- CMB-4 `combat.cs`：被爆擊時損耗該部位的護甲、嚴重失手損耗武器、打門損耗武器，接上原本只印訊息的 `damage.DamageEquipment`。嚴重失手的條件依原版是「對手不是被動生物」（原本相反，那個位元就是 Creature.passive）；`DamageEquipment` 檢查欄位改用傳入的欄位（原本用 `uimanager.CurrentSlot`）；爆擊加成亂數 0～31。
- CMB-5 `player/playerdatloop.cs`、`critterobjectdat.cs`：護甲值依 armor_val `品質 × 防護 / 64 + 1`（原本不看品質）；副手盾牌（0x3B～0x3F）加到身體與手；玩家防禦值改為防禦技能加上手中武器技能的一半（新增 `playerdat.DefenceScore`）。
- CMB-7 `combat.cs`：毒武器加成 `傷害 × (施法 + 30) / 40`（原本只加 0～1）；生物下毒：UW2 要 `rand % (毒 + 6)` 大於該部位護甲的兩倍，UW1 沒有護甲判定。
- CMB-8 `combat.cs` `PickBodyHitPoint`：依 pickloc 用攻擊中點判斷高過頭頂；`combat_missile.cs` 飛彈的攻擊頂端加上飛彈高度、結果加 4。
- CMB-9：弱點加倍上限 0xFF（原本 127）；`IsSharp` 排除彈弓而不是弓；特殊武器只在命中後觸發，前四種要求目標會動，開門武器開鎖後打開門。
- MAG-2 `damage.cs`：玩家（物品 127）的抗性加上法術與附魔給的位元（`PlayerDamageTypeScale`，原本沒人讀）；UW2 的抗性計算抽成 `CheckResistanceUW2`。
- CMB-6（盟友、追擊）屬於 NPC AI，留到 AI 類一起處理。
- 驗證：建置 0 錯誤、單元測試 291 通過 1 略過。遊戲內未錄影。

### 魔法（MAG-3～6、10、12，MAG-8 的物品冷卻，UPK-11 的學院樓層）
- MAG-3：
  - `World/uwobject.cs` `CheckIfObjectInValidSlot` 依 ObjWorn：護甲欄位包含靴子（原本 `< 4`）；副手是欄位 `7 + lefty`（原本左右相反）。
  - `player/playerdatloop.cs`：裝備附魔照 FixPlayerEquips 處理欄位 0～10 中穿戴的物品（原本漏掉左手戒指的欄位 10、手部欄位一律不算，盾牌附魔沒有效果）。
  - `spellcasting_class_0123.cs`：勇氣是 class 3 minor 10（原本寫成 0x10，永遠不生效）；UW2 的 minor 4 另外設位元 2。
  - `main.cs`：加速依 check_physics 讓移動物件只拿到一半影格，奇數影格留到下一輪（原本把減半後的值當餘數，等於沒有效果）。
- MAG-4 `spellcasting_class_7.cs` `SmiteUndead`：依 sp_ward_undead，有不死位元（check_res 回傳 0）才造成傷害（原本相反）；UW1 對任何物件有效（原本 UW1 什麼都不做）；UW2 浮空骷髏依 destroy_floatskull 變成普通骷髏（0xC3）並釋放所帶物品（新增 `ObjectRemover_OLD.ClearContents`）。
- MAG-5：持續時間依 set_curmagic（0x40 為 2d8+6、0x80 為 3d20+24，原本少了加值、0x80 用 3d24）；`playerdatloop.cs` 依 duration_check，剩 1 的法術才結束、其餘再減 1（原本先減再比，提早一輪），結束後只回頭檢查補進來的那一格（原本從頭重跑，前面的法術會被多扣）。
- MAG-6：
  - `utility/CallBacks.cs`：`RunCodeOnTargetsInArea` 依 process_area 重寫。隨機選格改為 `rand % (w*h+3) < 次數`（原本相反），最多 5 輪；邊界裁切照原版（y 用 `> 64`）；物件迴圈先記下下一個物件再呼叫。範圍高度的運算優先順序也修正了（原本 `1 + r << 1`，y 方向比 x 多一格）。
  - `spellcasting_class_6.cs`：範圍法術以施法者為中心（gronk_area，原本寫死為玩家）；UW1 次數為 rollem(3, 4)、半徑 2（原本次數 2、半徑 0）。
- MAG-10：
  - `MagicEnchantment.CastObjectSpell` 依 checkSpell：玩家使用物品施法時，施法者是玩家（原本是物品本身）。每 0x2FD 時鐘單位才能施一次（MAG-8 的物品冷卻）；冷卻中只播音效（UW2 另有訊息），不扣次數、不消耗卷軸。食物和藥水不受冷卻限制；讀較早存檔造成時鐘倒退時，不算冷卻中。
  - `spellcasting_class_4.cs`：治療依 healing／get_hp_back。UW2 治療施法者（玩家時顯示「你被治療了」），UW1 治療目標；生物的血量上限是平均血量（原本一律治療玩家）。
  - `spellcasting_class_0123.cs`：class 0～3 只對玩家生效（原本生物施放時套在玩家身上）。
- MAG-12 `spellcasting_objects.cs`：class 12 依 player_affected_by。護甲欄位對應該部位，其他欄位對應身體與手；防護加 `(minor & 7) + 1` 到命中修正；強韌加到第一個部位的護甲（保留原版的疏失）。原本只算頭盔與胸甲、少 +1，強韌也加到命中修正。
- UPK-11（部分）`playerdatloop.cs` `ManaRegenChange`：學院樓層改為 `(level − 1) % 8 + 1`，原本差一層。
- 驗證：建置 0 錯誤、單元測試 291 通過、1 略過。遊戲內未錄影。

### 魔法（MAG-7～9、11）
- MAG-7 `magic/spellcasting.cs`：UW1 的五個生物類法術（恐懼、驅除不死、盟友、毒、麻痺）依 nail_1area，玩家施放時直接作用在前方 4 格、半徑 2 內的第一個生物（原本要點選目標）。
- MAG-8：
  - `runicmagic.cs` 依 player_cast：UW1 技能判定為「施法 + 5」對「圈數 × 2」（原本用 UW2 的「施法」對「圈數 × 3」）。
  - Britannia 只擋第 3 圈以上，訊息為「施法沒有成功」（原本連第 3 圈也擋，訊息也不同）。
  - UW2 麻痺時不能施法。
  - `spellcasting.cs`：持續法術欄位已滿時，`do_spell` 拒絕，不扣魔並顯示「施法沒有成功」（原本照扣）。
  - 物品冷卻已在上一批處理。
- MAG-9：
  - `interaction/enchanting.cs` 依 sp_enchant 重寫主流程。法術表的附魔解碼後會接著充能（原本解碼後什麼都不做）。
  - 武器（效果 0～2、4～6）與護甲（0～6、8～14）的等級上限：武器是「(等級 − 8) / 4 + 施法 / 11」，護甲是「等級 + 施法 / 11 − 10」。原本兩者條件對調，位元遮罩也錯。
  - 未附魔的物品要「是數量物件或沒有內容」才能附魔，連結值改為 0x201（原本寫成十進位 201）；護甲隨機種類改為 `(rand % 2) << 3`。
  - 充能失敗的亂數範圍改為 0～0x3FF。
  - `spellcasting_class_13.cs` 地圖區域：半徑依 special_spells 7（施法超過 15 時再加「施法 − 13」，除以 5 再加 2），改為依距離標記圓形範圍（新增 `automap.MarkCircleOfTilesVisited`，原本用視野的 flood fill），成功時顯示訊息 0x113。
- MAG-11：
  - `spellcasting_class_8.cs`：UW1 召喚怪物依 creat_spell，編號從 lvl 到 2 × lvl − 1（原本少了 + lvl）。
  - `spellcasting_class_11.cs`：偵測怪物法術的範圍是 10（8 是追蹤技能用的）。
- 驗證：建置 0 錯誤、單元測試 291 通過、1 略過。遊戲內未錄影。

### 機關（TRP-11、12、14、16～18）
- TRP-11 `traps/a_skill_trap.cs`：做技能判定時難度也是 owner × 3（原本只在直接比較時乘 3）。
- TRP-12 `traps/a_hack_trap_changegoal.cs`：機關 62 依原版，連結是觸發者本人或 1 時，改的是觸發者的目標（原本改連結的物件，連結 1 時改到玩家）。
- TRP-14 `triggers/trigger.cs`：非生物觸發者（例如丟出的物品）依觸發器自己的旗標判斷。UW2 需要觸發器的 flag 9，UW1 只擋「有附魔但沒有 flag 11」的觸發器；原本看的是觸發者物件的 flag。
- TRP-16 `World/timers.cs`：計時觸發器依 update_animobj。距離量到觸發器的目標格（quality、owner），條件是 `< 8`（原本量觸發器所在格、`<= 8`）；不限物品編號（原本只處理 425）；計數到 0x7F 迴繞。原版也會算漫遊之眼所在的格子，這裡沒做（本專案沒有獨立的眼睛格座標）。
- TRP-17 `traps/a_jump_trap.cs`：依 set_jmp，水平速度未達門檻時完全不作用（原本只有重力受門檻限制）。
- TRP-18：
  - `hack_trap.cs` 機關 39 不再強制中止（它的連結是被改變的物件，只有連結到機關時才會繼續）。
  - `a_ward_trap.cs`：ward／tell 結束機關鏈（原本繼續），傷害為 `rand() × 施法 / 0x8000 + 3`。
  - `a_damagetrap.cs` 依 whack_thing：玩家受傷也經過抗性（原本直接扣血）；毒要先通過抗毒判定，值依 4 位元欄位截斷（原本取 Min 0xF，也不判定抗性）。
- 驗證：建置 0 錯誤、單元測試 291 通過、1 略過。遊戲內未錄影。

### 機關（TRP-9、10 部分、13、15、19、20）
- TRP-9 `traps/a_door_trap.cs`：依 TRAP_DOOR。UW1 一律先把門鎖換成機關連結的鎖的複本（沒有連結就只拿掉），再開關門，並結束機關鏈（原本 UW1 完全不換鎖）；UW2 先開關門，owner 不為 0 才換鎖並結束機關鏈。換鎖抽成 `ReplaceLock`。
- TRP-10 `traps/a_teleport_trap.cs`：UW2 傳送機關 fine x 第 2 位元會讓玩家迷路，自動地圖關閉，並在 map_scrap（pdat 0xF9）記下樓層。到達時找附近空格（find_good_x_and_y）已在「放置空間檢查與其用處」補上。
- TRP-13 `traps/a_hack_trap_qbert.cs`：月門依 do_qbert，最多挑 4 次，條件是目的地在玩家 3 格內（兩個方向都是）；樓層只依最後選到的那格扣一次（原本每次重試都扣）。最後站的格子依原版存在字組變數 108（新增 `playerdat.GetGameVariableWord`／`SetGameVariableWord`），原本寫到變數 0x1D1，而且只存一個位元組。
- 同時修正 `a_set_variable_trap.cs` 的 UW2 任務旗標運算（set_numbered_variable）：op 1 保持不變（原本清除），其他運算設為 `right > 0`（原本一律設 1）。
- TRP-15 `traps/a_create_object_trap.cs`：
  - 機率改用 `rand() × 0x3F / 0x8000`；UW1 原本會再擲一次，已拿掉。
  - 「附近已有產生的物件」以範本為中心（原本以玩家為中心）。
  - UW2 隨機怪物：基準是「(樓層在該界中的層數 × 3 + 城堡時鐘) / 9」，範圍「玩家等級 + 城堡時鐘 + 1，最多 16」。原本用界數，範圍上下限也寫反。
  - 產生的物件有自己的一份範本內容複本（只複製第一個，原本與範本共用）；動畫物件加進動畫清單。
- TRP-19 `interaction/trapdisarming.cs`：依 DetectedTrap／RemoveTrap 重寫。
  - UW2：找第一個 minor 3 觸發器。偵測難度是 10 + 2 × 界數，解除難度是 8 + 界數（原本兩者相同）。成功時從觸發器的目標格刪掉整串機關（原本只把觸發器的連結清成 0）。
  - UW1：看第一個機關物件，只有種類 0～2 能偵測和解除，難度都是 8。成功時從物件內容釋放；嚴重失敗而沒有觸發器時，執行機關後刪除它。
- TRP-20：
  - `traps/a_pit_trap.cs`：高度超過 15 才變成實心（原本 15 就封死格子）。
  - `World/tileinfo.cs`：UW1 改地形時，高度最多 13、牆壁材質 0x30 以上表示不改。
  - `traps/an_inventory_trap.cs`：UW2 沒有該物品時走「否則」分支（UW1 照舊停止）。
- 驗證：建置 0 錯誤、單元測試 291 通過、1 略過。遊戲內未錄影。

### 玩家狀態（UPK-3～10、UPK-11 大部分）
- UPK-3／4 `player/playerdat.cs` `ChangeExperience`：依 player_get_exp。
  - 損失經驗時扣掉、不低於 0（原本 `Exp - 負數`，死亡反而增加經驗）。
  - 只有 UW2 會把獲得的經驗減半；UW1 超過 0x17700 後不再增加。
  - 超出原本區域（UW2 看界數、UW1 看樓層）時減半再加 1。
  - UW2 超過 0x7FFF0 的那筆經驗整筆拒絕（原本封頂）。
  - 升級訊息顯示新的等級（原本顯示升了幾級）。
- UPK-5 `playerdatloop.cs`：慢速更新的週期 UW1 是 24 次（第 24 次時計數歸零）、UW2 是 30 次；飢餓每次減 3～6；清醒時 `skill_check(力量, 15)` 成功才回 1 點血（原本用 10 當難度，而且用了法術的回血公式）。
- UPK-6 `World/sleep.cs` 依 player_sleep：
  - 舒適度要飢餓 > 0x40；回血量是 `regen + regen × 舒適度 − 1`。
  - 一整夜的飢餓是 −24 −（rand & 0x1F），醒酒 −0x20；中斷時醒酒 −0x10。
  - 中斷時疲勞 UW1 減 0x20、UW2 減 0x18；只有 UW2 會讓將死的人撐到夢醒。
  - 光源燃燒改為以燃燒量計算（原本誤當成計數器，而且其中一處寫成 0x180）。
- UPK-7 `playerdatloop.cs` `UpdateLightStability(amount, counter)` 依 DegradeLights：
  - 燃燒速度取自光源資料表（原本用 classindex）。
  - 每次燃燒 `amount / r` 點；熄滅的門檻 UW2 是品質 1、UW1 是 0。
  - UW2 時間停止時不燃燒。
  - 最亮光源也算進游標上拿著的光源。
- UPK-8 `playerdatskills.cs` `IncreaseSkill` 依 get_skill：用屬性數值比較（原本用屬性編號 0～2）；UW2 的機率表是 {25, 30, 15}（原本用 UW1 的 {25, 40, 10}）；結果不超過 30。技能群組隨機選擇改為 `rand × 數量 / 0x8000`。
- UPK-9：`CanCarryWeight` 依 EncumCheck，算入容器內容（ItemWeight）。UW1 負重上限是力量 × 20（原本兩代都用 UW2 的公式）。
  - `RecalculateHPManaMaxWeight` 改為呼叫 `UpdateHPManaMax`，再另外算負重。原本兩個函式各算一次 HP 與魔力上限，前者沒有 Tybal 的規則。
- UPK-10：
  - 起始的疲勞與食物回血計數：UW1 是 0x40、UW2 是 0x30（原本食物回血是 0，UW1 疲勞是 0x30）。
  - 吃東西的回血 UW1 是除以 8。
  - 酒醉上限 0x3F（原本是 6 位元迴繞）。
  - 飲料回的 2 點血不超過上限。
- UPK-11：
  - UW1 在第 9 層不能睡。
  - Tybal 層的魔力上限依 player_compute，一律先存起來，不看寶珠。
  - 每 8／10 分鐘的 `yearly_checkup`：移動物件的吃飽位元隨機設定，3/4 機會清掉盟友位元。
  - 游泳計數只在 < 0x8C 時增加。
  - 學院樓層的計算已在魔法那一批修正。
  - 睡覺時的物件清除與生物平靜：見下方「UPK-11 其餘」。
- 驗證：建置 0 錯誤、單元測試 291 通過、1 略過。遊戲內未錄影。

### NPC AI（AI-1、3 部分、4～6、10、11）與對話的 npc_hunger
- AI-1 `traps/a_create_object_trap.cs` `FindAndRunCreateObjectTraps(isPlayer)` 依 DoWanderingMonsters／check_alert：清醒時只在玩家 8 格外產生，睡覺時全部（原本相反，只在玩家附近產生，也不分清醒或睡覺）。
- AI-3（部分）`npc/pathfind.cs`：UW1 路徑搜尋 32 步（UW2 是 0x28）。
- AI-4 `npc/npcai.cs`：UW1 的目標 3「貼近目標」依 seg007_1798_3E4。距離 1.4 格內面向目標並擺出第一招姿勢；超過 8 格時跳到目標旁邊（往自己的方向退四分之一）；中間距離就走向目標（原本是空的）。
- AI-5 `npc/pathfind.cs`：UW2 的路徑範圍一律是 5。原版判斷 10 的條件永遠不成立，我們原本在第 0 界用了 10。
- AI-6：排程事件 1 與對話匯出改目標時，經過 `npc.SetGoalAndGtarg`（critter_set_goal），守衛中的生物會記住原本的目標。
- AI-10 `interaction/damage.cs`：UW2 損壞有主人的物品時，依 damage_object／player_did_bad 通知主人那一族（`a_do_trap_trespass.HackTrapTrespass`）。UW1 原版沒有這條。
- AI-11 `utility/teleportation.cs`：UW2 抵達第 1 層時，依 gruesome_door_hack 把抵達格上的門解鎖並打開。原版會直接結束開門動畫，這裡可能會播放開門動畫。比對時原列為 UW1，實際上 UW1 沒有這段。
- CNV-7（部分）`conversationvariables.cs`：`npc_hunger` 改讀寫吃飽位元（b19 bit 7，`UnkBit_0x19_7`），匯入時吃飽是 0x10、否則 0xC0，匯出時 < 0x20 算吃飽。原本讀寫的是只有 6 位元的欄位，`& 0x80` 永遠是 0，判斷也相反。
- 驗證：建置 0 錯誤、單元測試 291 通過、1 略過。遊戲內未錄影。

### 對話虛擬機與內建函式（CNV-4、5，CNV-9 部分）
- CNV-5 `conversation/conversationvm.cs` 依 BABL.C：
  - 除以 0 和取餘數除以 0 都會彈出兩個值、推入 0x7FFF。原本除法沒彈出就推入，弄亂堆疊；取餘數則直接丟出例外。
  - OR／AND 改為邏輯運算（結果 0 或 1，原本是位元運算）。
  - 遇到未知的指令碼就結束執行（原本略過）。
- CNV-4：
  - 補上 STRCMP 指令（替換後比較，大小寫要相同）。
  - RESPOND 指令改為彈出字串、以玩家說話的方式顯示（原本不彈出，也不顯示）。
  - 新增內建函式 `plural`、`append`、`copy`、`find`、`val`、`add_to_npc_inv`、`end_barter`、`say`、`respond`（各一個檔案，在 `imported_functions.cs` 綁定）。
  - 對話結束時把交易格物品放回的程式，抽成 `end_barter` 共用，結束時也會清掉交易格。
  - `random` 改用 `rand() × n / 0x8000 + 1`。
- CNV-9（部分）：`compare` 先做 @ 替換再不分大小寫比較；`contains` 依 StringContains 比對整個字（前後是字串頭尾、空白或標點），並做 @ 替換。原版只有在沒有替換時才不分大小寫，這裡一律不分。替換時缺少的 C 類、@@、P 類索引未做。
- 驗證：建置 0 錯誤、單元測試 291 通過、1 略過。遊戲內未錄影。

### 對話內建函式（CNV-7、8 部分）
- `x_traps`：檢查的是值介於 0～0x3FF（原本檢查變數編號）。
- `set_inv_quality`：遮成 6 位元並回傳 1。
- `teleport_talker`：回傳 1。原版延到對話結束才移動，這裡仍是立刻移動。
- `give_ptr_npc`（交易格這條路）：
  - UW1 一律整個給出。
  - UW2 數量 ≤ 0 時回傳 0；數量物件比要求的多時拆出那個數量給 NPC。
  - 不在交易格時，原版從背包取出（invRemoveObject）。原本會把關卡清單裡同編號的物件接到 NPC 身上，弄壞物件串列；這裡先改為回傳 0，背包那條路已在「放置空間檢查與其用處」補上。
- `x_obj_pos` 依原版重寫：x、y、z 是傳址參數。模式 1 設定位置與高度（原本漏設 z）；模式 2 讀回所在格與高度（原本反過來去改 NPC 的家）；其他模式讀回 fine 位置與高度。
- `give_to_npc` 已在 CNV-1～3 修正。
- `remove_talker`／`transform_talker`：原版的 OBJ_HOMEX 是目前所在格，本專案的 `npc_xhome` 用法相同，查證後不能確認有錯，未改。
- 驗證：建置 0 錯誤、單元測試 291 通過、1 略過。遊戲內未錄影。

### 交易價值與脾氣（CNV-6）
- `Rng.RandomOffset` 改成原版的 range()：`base + base × (下限 + rand×(上限−下限)/0x8000) / 100`，乘積與結果都是 16 位元。
- 對話開始時（`conversationinitialisation.cs`）以 NPC 的物件編號當亂數種子，算完再重新以時間取種：
  - 貪婪（TradeThreshold）＝ 討價值 × 6 的 −25%～+25%。
  - 耐心＝ −20%～+100%。
  - 估價誤差＝ (15 − 精明) × 6 的 −25%～+50%。
  - 最後貪婪減魅力 × 2、耐心加魅力 ÷ 2。
- 物品估價（`GetTradeValueOfItem`，依 assess_value）：
  - 喜歡的 ×1.5、討厭的算 0（新增 `DoesNPCLike`）。
  - 數量物件（不是特殊連結）乘上數量；UW2 的金幣品質當 63。
  - UW2 用 32 位元算 `價值×品質>>6`，UW1 是 16 位元；最低 1。
  - 誤差以該物件的編號當種子，每次估同一件結果一樣。
- 雙方總值以 16 位元回傳；`do_offer`、`do_judgement` 的評估 `(玩家−NPC)×100/NPC` 也以 16 位元計算。
- 成交（`SwapTradedObjects`）：NPC 討厭的東西不收，留在玩家交易格；給 NPC 的金幣併入原有金幣堆（合計小於 999 時），否則放在物品欄最前面（新增 `NPCInventoryAdd`，`GiveItemIndexToNPC` 也改用它）。
- `do_demand`：
  - UW1 在玩家總值較高或 NPC 是盟友時成功；成功時若態度大於 0 就降一級。
  - NPC 那邊的總值不計喜好。
  - 心情：盟友 −1，否則態度小於 2 時 +1（原本看 npc_hunger 的 0x40）。
- `babl_hack` 8 乘的是貪婪（原本乘耐心）。
- `setup_to_barter`：交易格滿了之後換掉舊物品的機率改成 5/8（`rand() & 7 < 5`，原本 5/7）。
- 驗證：建置 0 錯誤、單元測試 291 通過、1 略過。遊戲內未錄影。

### 對話內建函式其餘部分（CNV-8）
- `place_object`：
  - x 為負時放在玩家腳邊（玩家所在格、同樣的位置與高度）。
  - 否則放在 (x, y) 格：物件保留原本的格內位置，高度是地板高度，接在該格物件串列的最後面（原本是隨機位置、接在最前面）。
  - 座標不在 1～63 時回傳 0。原版這時已經把物件從 NPC 身上取下、哪裡都沒放；這裡留在 NPC 身上。
  - 不在 NPC 物品欄的物件：原版照樣放，這裡回傳 0（那個物件還接在別的串列）。
  - 原版放之前用 can_place 檢查空間：已在「放置空間檢查與其用處」補上。
- `switch_pic`（UW2）：
  - 小於 0x100 是有名字的 NPC（whoami）；0x100 以上換成生物種類 n − 0x100 的通用頭像與種類名稱（原本一律當 whoami）。超過 0x140 用通用頭像 0。
  - 名字照對話開始時的格式（顏色、翻譯）；回傳 1。
- NPC 物品欄：原版在對話開始、執行腳本之前就生成（Converse）；本專案原本只在 `find_inv`、`setup_to_barter`、`take_from_npc` 用到時才生成，現在在對話開始就生成。門或鬼火的臨時說話者不生成（對話後就刪掉）。
- `teleport_talker`（UW2）：改為記下位置，對話結束時才移動（do_babl_teleport）。原版先傳送玩家、再把 NPC 移到玩家的新樓層；這裡先在原樓層移動 NPC，再傳送玩家。
- UW2 對話結束的 8 次動畫補跑（update_animobj）：本專案的門等動畫是即時跑的，沒有逐格動畫清單，未做。排程補跑（Sched_SetAllClocks）原本就有。
- 驗證：建置 0 錯誤、單元測試 291 通過、1 略過。遊戲內未錄影。

### 對話文字替換（CNV-9 其餘）
- `TextSubstitute`（`conversationio.cs`）依 convert_string 改寫成逐字掃描（原本用正規表示式，只認 G、S、P 三類）：
  - @ 之後是種類（G 全域、S 堆疊區域變數、P 傳址參數、C 常數），C 以外再接型別（I 整數，其他當字串編號），再接數字。
  - 數字後面可以再接一個變數當 1 起算的索引：G 讀 mem[數字 + 索引 − 1]，S 讀 stack[bp + 數字 + 索引 − 1]，P 讀參數裡存的位址 + 索引 − 1，C 就是數字本身。
  - 索引變數只認整數型別，否則當 0，而且它的數字留在文字裡（AtIndex）。
  - `@@` 是 @ 本身。
  - 替換出來的字串會再替換（原版沒有層數限制，這裡最多 8 層）。
- `contains`：原版把兩個來源字串轉小寫，搜尋的卻是替換後的副本，所以只有沒替換的字串不分大小寫；現在照這樣（原本一律不分）。原版是直接改掉來源字串，這裡不改。
- 新增單元測試 `ConversationSubstituteTests`（6 項，只測整數替換：G 加常數索引、S 當索引、S 正負編號、P 加索引、C 與 @@、字串型別的索引）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### 遊蕩與移動生物（AI-2、AI-8）
- 遊蕩（`NPCWanderUpdate`，crit_drunkwalk）：
  - UW2 站著的生物，只有在懶散值大於亂數、而且播到動作最後一格時才開始走動（原本條件顛倒，懶散值小於等於亂數時反而會走）。
  - 走動中撞到東西時向左或向右轉 90°（原本 `rand()%2 − 1` 只會轉 −90° 或不轉），同時更新細方向。
  - 飛行生物每次都換俯仰角：高度在 0xE 以上時 0xE～0x10，接近地板時 0x10～0x12，其他 0xE～0x12（原本高處不換）。兩代規則相同。
  - 站著且沒轉向時保留原本的細方向（UW1 不改；UW2 原版用未初始化的值）。
- 移動生物（`moveNPCToTile`，teleport_critter）：
  - 已經在目標格就不動、回傳成功。
  - 格內位置依地形：開放格在正中間 (4, 4)，斜角格在開放的角落；原版把西北斜角給了和東南斜角一樣的 (6, 1)，照做。原本固定 (3, 3)。
  - 飛行生物放在地板和高度 0x80 的中間（原本放在地上）。
  - 原版放之前用 can_place 檢查空間：已在「放置空間檢查與其用處」補上。
- 睡覺時找上門的怪物（`sleep.cs`，wander_that_monster）：放在地板上（飛行生物也是），並設定 b19 第 0 位（知道玩家在哪）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### UW1 特有 AI 規則與察覺（AI-3，CMB-6 部分）
- 想說話的生物（`NPCGoal_TalkTo`，crit_talk）：敵對時轉守衛（目標 4）只有 UW2 有，UW1 不轉。
- 撞到門（crit_head_for_loc）：
  - UW1 不敵對的生物從不開門，直接放棄這個方向；UW2 一樣是四分之一機率放棄、其餘試著開門。
  - UW1 停下來時保留動作格，UW2 歸零。
- 開門（`NPCTryToOpenDoor`，try_to_open_door）：秘密門開著或關著都不碰（`item_id & 7 == 7`，原本只排除關著的）。
- 可接受的危險（`GetCritterRange`，acceptable_danger）：UW1 第 6 層對話 0x16 的生物是 0。
- 察覺目標（`SearchForGoalTarget`，target_found，兩代相同）：
  - 邊界照原版：距離平方等於聽覺範圍平方的四分之一時不算「一定察覺」；等於視覺範圍時算看得到；等於聽覺四倍時算沒聽到。
  - 原本用 `Math.Pow` 算浮點數，改成整數。
- 守衛（crit_guard）聽到聲音時，`rand()%2 == 0` 才走過去（原本是 == 1；機率一樣，但亂數序列和原版對不上）。
- CMB-6 部分 — 加入戰鬥（critter_mv）：盟友一定加入；其他生物要玩家打的是同種族（不是自己）而且不是獨行的。原本 goto 鏈讓盟友永遠跳過。時間條件改成「打的時間 + 0x200 大於現在」（原本是大於等於）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### AI 雜項（AI-9）
- 經過時間後的生物（`ReturnNPCToHomeTile`，up_crit）：
  - 回血改成「目前和平均血量的中間」（原本加平均血量的一半）。
  - 也清掉 b19 第 1 位（聽到聲音）。
  - UW1 一律換隨機方向；UW2 想說話（目標 10）的不換。
  - 回到 quality、owner 記的家（原本拿目前所在格當目的地，等於沒動）。
- 排程關門（`FindAndCloseDoors`，DoClosingDoors）：開著的門關上的機率是 3/10（`rand()×10/0x8000 < 3`，原本 1/2）。
- 死亡留下的東西（`DropRemainsAndLoot`，build_corpse）：
  - 血跡等液體的 y 位置用生物的 y（原本誤用 z），品質 0x28。
  - 屍體 7/16 的機率，但 UW2 的 Pits of Carnage（世界 7）一定留下（原本在那裡反而一定不留）；擁有者設為生物種類。
- 偷竊（`thief.cs`，player_grabbed／critter_get_told）：
  - 訊息依新的態度：0 被激怒、1 感到不快、2 注意到你的行為（原本一律「被激怒」）。
  - 看到的生物算進 20 個的上限（原本永遠回傳 false）。
  - 物品變成無主：擁有者（低 5 位）在 0x1D 以下，UW1 是 0x1B 以下（原本是整個值 ≤ 29）。
  - 容器內物品的擁有者只有 UW2 清掉，而且不受上一條限制。
- 睡覺時找上門的怪物（`sleep.cs`）：
  - 只會來一隻（原本次數參數 0，等於不限）。
  - 停在離玩家還差兩格的地方（路徑最後一格是玩家所在格；原本只差一格）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### 追擊、守衛放棄、碰撞與繞行（CMB-6 其餘）
- 追擊（`AttackGoalSearchForTarget`，crit_offense_find_target）：
  - 目標不在原本要去的格時（x 或 y 不同；原本要兩個都不同）才以 1/8 機率重新察覺。
  - 隱約聽到時：UW2 一半機率走過去、否則放棄；UW1 反過來，一半機率放棄並繼續聽（b19 第 1 位），否則走過去。
  - 失去目標時：UW2 繼續聽；UW1 不聽，而且放棄後不再往下走。
  - 已經在目標格的判斷改用 x（原本兩邊都比 y）。
  - 走不過去（b18 第 6 位）時放棄目標（原本只清 b19 第 1 位）。
- 攻擊（`NPC_Goal5_Attack`，crit_offense）：
  - 守衛（舊目標 4）離家超過移動範圍的兩倍（`range² × 4 < 離家距離²`）才放棄（原本用士氣的平方）。
  - UW1 近戰後不結束，會接著判斷放棄和追擊（UW2 打完就結束）。
- 移動（`NPC_Goto`，crit_head_for_loc）：
  - 到達時速度歸零（原本設成 0x13 的第 7 位）；UW1 保留動作格。
  - 撞到生物：兩隻都在攻擊（目標 5）、而且對方不是玩家時互相不理會；其他都放棄這個方向（原本條件顛倒，撞到玩家才不理會）。
  - 飛行生物穿過開著的門那一回合不前進（原本旗標從沒設定）。
  - 走路動畫的格數循環改成完整長度（原本少最後一格）；換成走路時，原本在倒數第二格以後就從頭開始。
- 繞行玩家（`VectorsToPlayer`，crit_avoid_player）依原版重寫：玩家在範圍內（原本顛倒成範圍外）、從生物自己的位置算（原本用目標的位置）、方向差在 90° 以內才偏 45°（原本 `||` 讓條件永遠成立，還拿錯變數）。
- 逃跑（`NPC_Goal6`，crit_flee）：
  - 高度差取絕對值。
  - 卡住時轉 90°（± 兩個八分之一）再加 0～31 的亂數；原本只轉 45°、用 % 5，而且亂數也被一起左移。兩個亂數的先後順序沿用原始碼的寫法（左邊先），未對照組語確認。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### 城堡作息（AI-7，UW2）
- `a_hack_trap_castleschedule.cs`（move_folks_around／where_shall_we_hang_out）：
  - 時段用 24 小時制（`遊戲時鐘 / 0xE1000 % 24`），每兩小時一段，第 0 段是凌晨 1～2 點（原本用 12 小時制，白天晚上分不出來）。
  - 第 3、5、9 段原地不動（原版設定了廚房範圍但掉進 default；原本去王座廳）。
  - Miranda（0x88）一律在自己的位置，城堡劇情還沒開始（X clock 1 為 0）時所有人都在自己的位置（原本 Miranda 的條件顛倒：劇情開始後才留在原位）。
  - 位置 5 是留在家（quality、owner）；原本和位置 4 一樣停在目前所在格。
  - 範圍內的隨機格包含兩端（`rand() % (x2 − x1 + 1)`，原本少最後一排）。
  - 守衛 0x81 也照原版排程：原版讀到兩個表格之外，結果是 x = 0x22、y = 0（原本跳過 0x81）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### 放置空間檢查與其用處（CNV-7、CNV-8、AI-8、TRP-10 其餘）
- 新增 `motion.CanPlace`（`physics/motion_collisions.cs`，can_place）：用本專案移植的原版碰撞函式組成，地形檢查 `ProcessMotionTileHeights_seg028_2941_385`（TerrainCheck）、物件檢查 `ScanForCollisions`（ObjectCheck）、排序 `SortCollisions`（process_objlist）。用自己的計算紀錄，結束後還原。
  - 物件頂端超過 0x7F（天花板）不行。
  - 腳下有牆或太高的地板（0x300）不行。
  - 有東西擋住不行；要站在物件上時，那個物件必須是實心的。
  - 不是飛行的，懸空超過 range 不行。
- AI-8 `moveNPCToTile`（teleport_critter）：放之前檢查空間（range 8），沒位置就失敗。睡覺時找上門的怪物用新的 `onFloor` 參數，在地板高度檢查（wander_that_monster）。
- CNV-8 `place_object`：放到格子前，在格子角落、地板高度、以物件半徑 + 4 檢查空間（飛行），沒位置回傳 0、物件留在 NPC 身上（原版這時已經取下）。放下後用 obj_deal 的移植 `PlacedObjectCollision_seg030_2BB7_10BC` 安置（可能掉落、沉沒或毀掉）。
- TRP-10 傳送找空格（`teleportation.cs`）：
  - 新增 `FindGoodXAndY`（find_good_x_and_y）：從目標格往外廣度搜尋，只走各格開放的那幾邊，每圈最多 20 格，範圍是目標周圍 10×10 格。每格在中央 (3, 3)、地板高度（斜坡加 4）檢查玩家放不放得下。原版記錄走過哪些格的陣列只有 9 欄，第 10 欄會寫出界，這裡用 10 欄。
  - 同層傳送（do_teleport）：不是第 63 格時要先找到空格，找不到就不傳送（回傳 2）。
  - 到達時（new_player_pos）：先嚴格找，找不到再清掉物件找一次；清除時每格先刪掉有高度的物件和移動物件（照 culling 規則，玩家除外）。兩次都找不到時玩家死亡（血量 0）；原版這時不放置玩家，這裡仍放在目標格。
- CNV-7 `give_ptr_npc` 背包那條路：不在交易格時，從玩家背包拿出 qty 個（UW2 只拆普通的數量物件，其他整個給；UW1 qty 為負或 0 時整個給），移到關卡物件清單後用 `NPCInventoryAdd` 給 NPC（金幣會併堆）。交易格拆出來的那部分也改用 `NPCInventoryAdd`。背包物件的編號來自 `find_inv(1, …)`；交易格先比對，和原版順序相同。裝備欄的效果（FixPlayerEquips）沒有另外處理。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。`CanPlace` 和找空格沒有單元測試（要載入整張地圖），遊戲內未錄影。

### C1-1 物件物理（OBJPHYS.C，seg030）
- 比對方式：5 個原版檔案（COLLIDE、MOTION、OBJPHYS、PHYSICS、PLAYMOVE）各由一個唯讀子代理列出差異，再逐條對照原版確認後修正。
- set_phys_data（`motion.cs` `ApplyProjectileMotion`）：
  - 熔岩火傷是 1/5 機率（原本每一步都受傷，等於 5 倍）。
  - UW1 撞擊傷害門檻 0x100，不依種類縮放；UW2 是 0x180，生物 ÷4、血量低於 0x20 的物件 ×4。
  - UW1 撞擊後把血量再寫回一次（原版就是這樣，非致命的摔落傷害會被抵銷）。
  - 靜態物件變成移動物件的條件加上重力。
  - 重力位元：UW1 只有重力恰好是 −4 才設。
- obj_deal（`PlacedObjectCollision_seg030_2BB7_10BC`）：
  - 熔岩：品質類別 3 一定留下；其他物件抗火才留下（原本顛倒：類別 3 要抗火，其他一律毀掉）。
  - 水：只有 UW2 濺水花（並處理 filanium 油）。UW1 沒有冰的情況。
  - 從邊緣滑下：方向是原本方向加上 0xC0 + 亂數（原本直接設成絕對值）。
  - 隨機速度 1～4、俯仰 0xE～0x11（原本少一）；UW1 滑下後也套用。
- 碰撞推動（bounce_obj）：質量比是「推的物體 ÷ 被推的物體」，16 位元運算（原本是被推的物體自己除自己，永遠 1:1）。
- 物件落地（mob_to_static）：
  - 熄滅的燈只有 in-class 4～6（原本到 7）。
  - 亂數改用 `rand() & 7`。
  - UW1 第 9 層（Ethereal Void）落地的東西一律不留。
- mob_init（`ObjectCreator.InitMobileObject`）：非生物清掉最後攻擊者（原本留著前一個佔用者的值）。
- 飛彈命中（`combat_missile.cs`）：命中格的判斷原本顛倒（objhit_used 時用被打物件的格）；擊中生物後設位元只有 UW2。
- 物件相撞觸發（do_objhit）：UW1 撞到任何機關或觸發器（major class 6）都會觸發，UW2 只限觸發器。
- 未做：
  - do_objhit 用全域 MapObj_X 判斷「使用後物件被毀」：本專案的 `use` 沒有對應的全域值。
  - 靜態物件清單滿時要丟棄物件：已在「C1-1 其餘：物件清單滿時的配置」完成。
  - 動畫物件換清單時的 Change_AnimPtr：已在「C1 其餘：Change_AnimPtr」完成。
  - UW1 護符在熔岩裡爆炸的 fireball_effect 與高度遞增：已在「C1-1 其餘：聖物丟進熔岩」完成。
  - 原版其他呼叫 obj_deal 的地方：已逐一對照完成。
    - 碎片、掉落、碰撞、建立物件法術、對話放置物件、丟出物件：原本就有。
    - 生物死亡的血跡（build_corpse）：改在生物的高度生成，再用 obj_deal 安置（可能掉落、沉沒或燒掉）。原本直接放在地板高度。
    - UW2 販賣機（vend）：品質 0x3F，在高度 0x76 生成，再用 obj_deal（how 0）讓它掉下來。原本直接放在地板上，品質是預設值。
    - UW1 翡翠謎題的 Vas 符文石（emerald_trap）：細位置 3, 3，高度 0x40，再用 obj_deal 安置。原本是細位置 6, 6，停在半空。
    - UW1 死亡留下的骨頭（player_is_dead）：新增 `LeaveBones`。0xC2～0xC6 隨機一種，放在倒下的位置與高度，主人 0x3F（「一名冒險者」），再用 obj_deal 安置。原本沒有。
    - UW2 寶石洞的血跡（do_gem）：put_at 之後再跑一次 obj_deal。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### C1-2 地形與物件碰撞（COLLIDE.C，seg028）
- TerrainCheck（`ProcessMotionTileHeights_seg028_2941_385`）：第三個角（+x、+y）的旗標 `SubArray.UnkF` 改讀 16 位元（原本 8 位元，牆 0x200、太高 0x100、落差 0x800 都被丟掉，該角可以穿進牆或懸空）。
- ComputeHeading（`seg028_2941_803`）：
  - 單一擋住的角在斜角方向的判斷是「方向為奇數」（原本寫成 `/ 2`）。
  - 正對第 3 個角時用 `8 − x`。
  - 開放方向用 x、y 兩個總和（原本 y 用了兩次；這個值原版沒有人讀）。
- get_home_tile（`GetTileAttribute8_seg028_2941_A6D`）讀中心格（原本讀東南鄰格，貼牆斜角滑動幾乎不會觸發）。
- ObjectCheck（`ScanForCollisions`）：每格最多 64 個物件（原本 65）。
- 新增 `motion.PutAt`（put_at／drop_around_place）：在 ±range 內隨機試 24 個位置（`stayCentered` 時先試原位），用 `CanPlace` 檢查，接在該格串列最後，靜態物件用 obj_deal 安置；都不行時放在原位，除非被 culling 掉。
  - 屍體改用它（near_mob_put_at，範圍 4、從生物的高度開始），原本固定放在生物位置的地板上。
- 未做：
  - 容器灑出（TREASURE.C 用 put_at，另有擁有者、燈、鎖的規則）：已在 C2-4 完成。
  - 原版其他用 put_at 的地方：除了地震法術掉下的東西，已在「C1-2 其餘」完成。
  - CanPlace 輸出 nvokHgt／nvokTerr：已在「C3-5 其餘：單步移動」完成（`motion.NvokHgt`／`NvokTerr`）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### C1-3 移動計算（MOTION.C，seg031）
- do_physics 迴圈（`CalculateMotion`）：
  - 卡住的判斷用遞增前的次數（`trycnt++ == 0x10`），允許 16 輪（原本 15 輪）。
  - stop_me（`ZeroiseMotionValues_seg031_2CFA_7BF`）把「已走步數」設成「總步數 + 1」結束移動；原本把總步數加一、已走步數歸零，移動會繼續沿舊方向走。
- check_positions（`DoTileCollisionMaybe_seg031_2CFA_179C`）：腳下沒東西（0x1000）就開始掉落，和有沒有撞牆無關（原本包在撞牆的判斷裡，物件和生物走下邊緣不會馬上掉）。
- get_pcoll（`GetCollisionHeightState_seg031_2CFA_13B2`）爬階與落腳依原版重寫：
  - 平地上「靜止且腳下有地面」的條件原本顛倒（地面位元 4）。
  - 成功爬上時設 hit_flag，之後才會重新排序並對新擋路的物件做碰撞（原本永遠不會執行）。
  - 只能站上實心的物件，而且要允許爬（原本傳錯成碰撞編號，條件也反了）。
  - 和實心物件同高時，只在允許爬時才算站在上面。
  - 站在物件或地面上時清掉落差位元 0x800（原本條件顛倒）。
  - 上台階到最高的地板時清掉 0x100（原本留著，隨後又被當成撞到台階彈回）。
- do_zbounce（`DoCollision_seg031_2CFA_D1F`）：
  - 落到非實心物件上時，看的是移動者自己是不是生物（原本看被落上的物件）。
  - UW2 冰上恢復速度的條件原本兩項都顛倒。
  - 從碰撞表移除紀錄時，用最後一筆補上（原本拿表尾之後的舊資料，最後一筆真的紀錄會遺失）。
  - UW1 落水：地形第 0 位元、而且要正在下降（UW2 是 `& 3 == 1`、下降或靜止）。
  - 撞擊音量是有號數（原本轉成 byte，負值變得很大聲）；16 位元乘法與位移照原版會溢位。
- bounce_that_guy（`MaybeReflection_seg031_2CFA_CC6`）：往上的速度 UW2 0xBC、UW1 0xEB（原本 0x8C）；隨機轉向 UW2 是 1/4（原本 3/4），UW1 一定轉；最低速度條件改成小於 0xEB；hit_obj 只有 UW2。
- space_to_motion（`InitialMotionCalc_seg031_2CFA_412`）：
  - z 速度超出 16 位元時改用垂直速度 × 時間（原本條件 `< 0` 永遠不成立，陡降時 z 速度變號、往反方向移動）。
  - 主軸速度為 0 時兩個軸的步進都設 1。
  - 16 位元 abs（`abs(-0x8000)` 仍是 −0x8000），也避免 C# 的 Math.Abs 在 −32768 拋出例外。
- full_move（`MAYBEGRAVITYZ_seg031_2CFA_1138`）：`zspeed << 5` 照原版是 16 位元。
- 欄位型別：剩餘時間 `speed_12` 改成 16 位元（原本只讀 1 個位元組，時間可能變號而繼續移動）；目標高度 `CollisionZposHeightRelated_dseg_67d6_419`（MP.targz）改成有號數（物件底下可以是負的）。
- rehead（`HeadingRelated_seg031_2CFA_A49`）：UW1 沒有新方向的斜角貼牆位置；方向沒變時 UW1 用 gx = 方向第 14 位元、gy = 1，一半機率兩個都反過來。
- IsaDoor（`FindClosedDoorCollision`）：從第一個擋路的紀錄開始找（原本從表頭）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影；這些是移動的核心，最需要實際走動、跳躍、丟東西確認。

### C1-4 玩家物理（PHYSICS.C，seg008）
- phys_affect_player（`ApplyPlayerMotion`）：
  - 貼牆滑動時臉的方向往移動方向轉：差值用 16 位元計算，方向原本轉反了；UW1 每次 0x400、UW2 0x600；UW1 一定會轉（原本因為 `slide` 預設 0 而永遠不轉），UW2 只在不在斜坡、水流時轉。`slide` 預設改為 −1。
  - UW2 正面撞牆只有在不在空中時才停下（原本空中撞牆也會停）。
  - 同一格裡改變高度時，壓力板的「離開」用舊高度判斷（原本先改了高度，離開永遠對不上）。
- set_player_phys_params：UW2 冰上斜坡加速時抓地力減 4（原本加 4）。
- do_player_input（`CalculateMotionFromCommand_seg008_1B09_108E`）：跑步跳只在完全靜止於地面時才跳，否則什麼都不做（原本會落到一般跳躍）。
- player_sqhandler（`motion_handlers.cs`）：走得慢時在邊緣停下，UW2 游泳或在冰上不停，UW1 都停。
- parse_player_terr（`ProcessPlayerTileState`）：UW1 沒有冰上的動作狀態。
- newFPS：負重的乘積是無號 16 位元（重量 683 以上會溢位，原版就是這樣）。
- player_setup（`InitialisePlayerOnLevelOrPositionChange`）：格內位置 3, 3（原本 6, 6）、UW1 動作序列 0x2C、品質清 0。
- UW1 Ethereal Void 特效（`etherealvoid.cs`）：血量 21～50 時是 1/4 機率扣血（原本 1/3）；畫面搖晃是 11/12 機率（原本每次）。
- 未做：simple_fizix／player_simple_move（一步一格的移動鍵，即 classic 的大寫 W／S／X，見 C3-5）。phys_bounce_up 原本就在 `CastClass0123_Spells`；換格時的燈光在每次換格的狀態更新重算，與 change_GrSq 相同；UW1 麻痺時的輸入阻擋沿用 UW2 的寫法（效果相同）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### C1-5 玩家輸入與移動迴圈（PLAYMOVE.C，seg035）
- 玩家噪音（`ApplyPlayerSneakScore`，set_sound）：移動時是 `速度 × 10 ÷ 跑步速度 + n − 5`（原本把速度除以 10 再除，結果永遠是 0，跑步時最多安靜 10 級，影響 NPC 聽覺）。
- 腳步聲（`FootSteps`，make_noise）：
  - 音量變化是 `速度 ÷ 32 − 16`（原本 `+ 15`，大約大聲 31）。
  - UW1 只要在移動就有腳步聲（UW2 才要速度超過 0x2F）。
  - UW2 游泳划水聲的音量同樣依速度（原本 0）。
- 畫面搖晃（`WalkOnSurfaceType`，parse_effect）：
  - 計數改成無號位元組，到 0 之後才結束（原本有號，128 以上的長度會立刻結束，最後一下也少了大幅晃動）。
  - 戰鬥搖晃強度是剩餘長度 ÷ 8（原本 ÷ 10）。
  - 搖晃量加在游泳搖擺之上（原本直接蓋掉），冰上滑動的額外偏擺也保留。
  - 亂數改用 `rand()`。
- 側移時的鏡頭晃動表少了一個逗號（`-3 - 4` 變成 −7），改正。
- Roaming Sight（漫遊之眼）期間移動鍵只控制眼睛，身體沒有輸入但物理照常（掉落、水流等），原本身體完全不動。
- 跳躍鍵：只有在地面上而且不在游泳時才跳；在空中或水中不跳（原本在漂浮或飛行時改成往上飛；往上飛另有按鍵）。
- 未做：
  - 物理時鐘：已完成，照 check_physics 加實際經過的時間（最多 0x40）。PIT 計時「較慢」的原因是每次累加都丟掉不足一 tick 的餘數（60 Hz 下每格只算 4 tick，實際是 4.27），改成保留餘數後就是每秒 256 tick。主迴圈每 0.0977 秒跑一次，增量大約就是原本固定的 0x19。
  - 一步一格的移動鍵（player_simple_move）及其腳步聲與噪音：已在「C3-5 其餘：單步移動」完成。finish_player 只用在醉倒（drop_drunk_player），本專案的 `motion.RunPlayerMotions` 原本就照它做。
  - 離開水面時停止水聲：已在 C6-3 完成。
  - UW1 Wizard Eye 時鍵盤仍移動身體、只有滑鼠移動眼睛：已在「C3-5 其餘：遙視與指標限制」完成（UW2 的按鍵移動眼睛，UW1 移動身體）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### C2-1 使用分派、鎖與法術充能（OBJUSE.C，以及骨頭）
- 比對方式：6 個唯讀子代理分別比對 OBJUSE、USEITEMS 前後半、INVDATA／INVPANEL、BAGS／TREASURE／COMBINE、撿取與丟下（INTERACT 等），再逐條對照原版確認後修正。
- UseObj（`use.cs`）：魔杖、食物（含 UW1 的藥水）、UW2 的藥水（0xE1～0xE7）、書本由自己處理機關與法術，使用後不再觸發使用機關、也不施放法術（原本地上的藥水和食物可以一直施放裡面的法術、扣充能而不被吃掉）。
- 骨頭（新增 `objects/bones.cs`，UseUtil／UseBonesOn）：骷髏、骨頭、骨堆（0xC2～0xC6）從物品欄使用時要選目標。
  - UW1：Garamon 的骨頭（擁有者 0x3E）用在他的墓碑（特殊連結 0x21）上會安葬他：設定護符與 Garamon 旗標、墓碑連結改成 0x222、啟動他的對話（whoami 0x1B）、觸發 (0x36, 0x34) 格的觸發器。原本完全沒有，`IsGaramonBuried`、`CanTalismansBeDestroyed` 永遠不會成立，護符丟進熔岩毀不掉，UW1 主線無法完成。他的骨頭用在別的墓碑上留著（0x103），其他骨頭用在墓碑上會安葬（0x86）。
  - UW2：用在墓碑上安葬（0x94），其他沒有效果（0x92）。
- 物品類別 3 的分派：植物 0xCE、0xCF 是 minor 0（原本寫在 minor 2，實際抓到的是 0xEE、0xEF 兩顆石頭）；UW1 死掉的腐屍蟲 0xD9 可以吃。
- 鎖（`a_lock.CheckLock`，checkLock）統一規則，結果 0～5：
  - 鑰匙要對上鎖的編號（`link & 0x1FF`，原本 `& 0x3F`；容器原本任何鑰匙都能開關）。
  - 開著的門或容器用鑰匙回答「已經開著」。
  - 解鎖時觸發門裡的解鎖機關（原本拿鎖物件的 link 當串列，等於觸發錯東西），之後刪掉鎖物件，除非鎖的 flag bit 1（id bit 10）允許再上鎖。
  - 開鎖：難度 0xF 的鎖開不了；0xE 要技能（UW2 含加一）至少 0x20（UW1 0x1F）。
- 鑰匙（`doorkey.cs`）：訊息是結果 + 2；解鎖後門不會自己打開（原本會打開）。
- 開鎖器（`lockpick.cs`）：UW1 用技能值本身、UW2 用技能 + 1；只有 UW2 會失手弄斷（再過一次敏捷 20 的檢定）；UW2 沒有開鎖技能時對沒上鎖的東西也說「失敗」；開鎖不會打開門。
- UseKey 分派：開鎖器是 0x101，其餘 0x10F 以下都是鑰匙（原本 UW2 把 0x100 當開鎖器、0x10E 用不了）。
- 兩個物件的使用（use-on）之後不再觸發目標的使用機關（原版沒有）。
- Open 法術（`spellcasting_class_7.cs`）：用技能 45 的開鎖規則，訊息是「法術打開了鎖」或「沒有明顯效果」。
- 容器（`container.cs`）：上鎖的在物品欄裡也打不開（原本只檢查地上的）。
- 門（`door.cs`）：生物打不開釘死的門（owner 第 0 位元）；UW2 推上鎖的門有聲音 0x2D。
- 法術充能（`MagicEnchantment.cs`）：
  - 只有「使用時施放」的法術（id bit 11）才扣充能；充能用完時只有 4/10 機率刪掉法術（原本每次都刪）。
  - 刪除時從串列中移除那一個（原本會把整串內容或前一個物件的內容清掉）。
  - 充能 0 的法術 4/10 機率當作沒有（decode_obj_spell），查看（look）時一定看得到。
  - 只在物件的內容裡找，不往內容的內容找。
- 一般物件施放法術（`UseItemCastSpell`，checkSpell）：只施放 id bit 11 的法術；冷卻只用在物品欄使用；在地上碰沒壞的魔杖不施放。只有機關不施放（原本武器、門、家具、動畫物件都排除）。
- 魔杖（`wand.cs`，UseWand）：從物品欄使用時先觸發使用機關再施放；之後若裡面已經沒有法術物件就裂開（包括原本就沒有的）；壞掉的魔杖什麼都不做。
- 開關的「已開則用第二個機關」規則只有 UW2（`trigger.cs`）。
- 未做：
  - checkSpell 在地上以物件為施法者、物品欄機關觸發後刪除：已在「C2-1 其餘」完成。
  - 動畫物件的使用：UW1 銀樹已在「C2-1 其餘」完成；移動中的門本專案不會變成動畫物件，不適用。
  - 充能數 n 與 0x3F 上限：`enchanting.MagicObjectChargeUpdate` 原本就照 useNSpellCharges（附魔與 babl_hack 7 都用它）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影；UW1 Garamon 安葬需要遊戲內確認。

### C2-2 物品的使用（USEITEMS.C，第一批）
- 油瓶（`oilflask.cs`，UseOilOn）：只能用在物品欄裡的東西；做火把或補油之後油瓶會用掉（原本永遠用不完）；燈籠的判斷是 0x94（原本寫成十進位 94）；UW2 從一疊木頭拿一塊做成品質 0x28 的火把，UW1 整個物件直接變火把、品質不變；其他東西「不能用油」（UW2 0xC0、UW1 0xB1）。
- 喝東西（`food.cs`，UseFood）：
  - 先印出味道（UW2 0xFC 水、0xFD 酒、0xFE 麥酒；UW1 0xED 水、0xEE 波特酒、0xEF 麥酒）。
  - 只有營養值介於 −0x7F 和 −1 之間（不含）才算酒精，水不會讓人喝醉。
  - 大失敗先印「酒精讓你倒下睡著」（UW2 0x100、UW1 0xF1），醒來（血量不為 0 時）才印訊息並搖晃畫面；一般失敗搖晃長度是醉度 ÷ 6（原本 + 10）；UW1 的訊息編號 0xF2、0xF3。
  - UW1 麥酒（0xBA）是飲料，原本被當成藥水。
  - 只有 UW2 有喝的聲音。
- UW1 毒菇（toadstool）：味道 0xE7，中毒值至少 4，否則加 2、最多 0xD（原本當普通食物）。
- 蘑菇：智力檢定不是一般失敗時恢復固定 0～2 點法力（原本依結果用百分比公式，失敗也會恢復）。`ManaRegenChange` 的 0 也算固定值（restore_mana）。
- 其他食物：游標上的一疊不能吃（UW1 印 0x77）；UW1 吃東西沒有聲音；味道的亂數改用遊戲的 `rand()`；UW2 只有 0xB0 的肉留下骨頭。
- 鑰匙寶石（UW2，`smallblackrockgem.cs`）：任何擁有者都算處理過（原本只認 1）、目標要在地上，並把寶石記到遊戲變數 6（大寶石的顏色會讀）。
- Orb Rock（UW1，`orbrock.cs`）：從物品欄用在寶珠上會用掉；削弱 Tybal 只對第一個（gronk_whoami 的 all 為 0）。
- Key of Infinity（UW1，`key_of_infinity.cs`）：只有用在顯示「有花紋的門」材質（地形 0xB）的材質物件 0x16E 上才有效，會用掉鑰匙並觸發開啟機關；其他都「沒有效果」（0x84）。
- 碎石鎚（`rockhammer.cs`）：碎成 1～4 塊（越大的石頭越多），每塊小一或兩級，最小的變成 3～8 顆投石；用 `motion.PutAt` 放在原處附近（原本固定一塊、而且 `Next(1,2)` 永遠是 1）。UW1 複製原石的資料再改物品。
- Tym 符文（`runetrap.cs`）：玩家麻痺 4～19（原本 4～18），並立刻停下；生物的目標值照原版存成 byte。
- 燈（`light.cs`，UseLight）：
  - 用完的判斷 UW2 是品質 ≤ 1、UW1 是 0；先檢查品質再移動。
  - 只有沒點的火把、蠟燭、燈籠、光球（UW1 細蠟燭）會被移到光源格；沒有空格時說「你的手滿了」。點著的在別處就直接熄掉。
  - 從地上使用印「燈只能裝備後使用」；只有 UW2 有點燈的聲音。
- 釣魚釣到的魚品質 0x3F；UW2 懷錶在 12 點時顯示 12（原本 0），結尾加句號。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### C2-3 容器規則、合成與物品欄（INVPANEL／INVDATA／BAGS／COMBINE）
- 放進容器（`container.TestContainerCanHold`，ItemFitsSlot）：
  - 先檢查重量：只算內容物加上新放的（原本連容器本身也算），容量 0 表示不限（原本當成 0，箭袋和符文袋永遠「太滿」）。
  - 指定物品的容器只收那一種（原本顛倒成那種不收）。
  - 箭袋：投石、弩箭、箭（0x10～0x12），UW2 也收魔杖（原本判斷成護甲類）。
  - 卷軸／地圖盒：UW1 是 0x138～0x13F，UW2 是 0x134～0x137、0x139、0x13A。
  - 碗：UW2 收食物（不含三種飲料）與 0xCE、0xCF、0x114、蠟、棍子、骨頭；UW1 收食物（含飲料）與 0xCE、0xCF、蠟燭、水蛭、燉菜、死腐屍蟲（原本 0x80～0xBF 都收）。
  - 鑰匙圈（UW2）：鑰匙但不含 0x100，也收另一個鑰匙圈。
  - 符文袋：符文與 0xE8～0xEF。
- 合成（`cmbloader.cs`，ObjsBeCombinable／CombineObjs）：任一方是多於一個的一疊或裝著東西就不合成；產物是新的物件，被用掉的手上物件換成產物放在游標上，否則放在物品欄那格；沒有標記用掉的來源保持原樣（原本會把保留的那件變成產物，例如點著的火把加玉米變成爆米花）。UW1 薰香的特例因此不需要了。
- 合併成一疊（AddToOccupiedSlot／AddTogether）：
  - 規則：同一種、可堆疊、都沒有內容物或特殊連結、鑰匙要同一把鎖、合計小於 999、品質同一級（÷16）且不能一個壞掉一個沒壞（投石、弩箭、箭例外）；UW2 儲存水晶不合併；還要能放進那一格。
  - 合併時把重量加回玩家（原本沒加，拿起再放回會讓重量永久變少）；品質取兩者平均（原本依數量加權）。
- 拆一疊（`DoPickupQty`）：拿走的部分扣重量（原本沒扣）；預設數量是 1（原本整疊）；拆出的物件清掉複製來的 next。
- 其他：`CanCarryWeight(item_id)` 原本把已攜帶重量算兩次；Armageddon 用的 `ClearInventory` 原本漏了第 18 格；裝備磨損「只限武器」的判斷是類別 0（物品 0～15，原本到 63，盾和弓也會被當武器磨損）。
- 未做的四項（巢狀袋子容量、熄燈、武器手、搜尋順序）已在「C2-3 其餘」完成；容器灑出的擁有者與鎖的規則已在 C2-4 完成。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### C2-4 戰利品與容器灑出（TREASURE.C、DumpTheBag）
- 生物戰利品（`npcloot.cs`）：
  - 寶物：亂數改用遊戲的 `rand()`（原本 `new Random()`）；UW1 的種類依樓層算（`rand() % (40 − 3L) − (33 − 3L)`），UW2 依世界；價值照原版存在有號位元組裡放大（≥12 是 ×8 − 68，≥8 是 ×4 − 20，≥4 是 ×2 − 4，原本公式和優先順序都錯，寶石價值 25 以上原版會變成負數，固定給 1 個）；`機率 × 4 < 價值` 時以 `rand() % 價值 < 機率 × 4` 給 1 個（原本判斷顛倒），否則 4d(2 × (4 × 機率 ÷ 價值)) ÷ 4。
  - 武器和其他戰利品的品質真的寫進物件（原本算了沒用）；一半機率是樓層 × 4 起的 4～8 倍、一半是 0～63（原本 1～63、而且兩種的順序反了）；UW2 品質類別 0xF 的固定 40（原本 0x40，而且 UW1 也套用）。
- 容器灑出（`container.cs`，DumpTheBag／drop_link_chain）：
  - 每個物件用 `motion.PutAt` 放在容器位置附近（範圍 6），原本隨機放在同一格、不檢查空間。
  - UW2：先移除鎖；內容物給容器的擁有者（可以有擁有者時，原本一律清成 0）；點著的燈熄滅；掉出來的觸發器會觸發（使用觸發）；通知擁有者（偷竊判定）；印「你倒空了…」或「…是空的」。
  - UW1：擁有者給容器本身；只在空的時候印「…是空的」。
  - 不再把容器換成打開的圖（原版灑出時不換）。
  - 生物死亡時掉出的東西屬於牠的種族（drop_some_objects）。
- 鎖的移除（`a_lock.RemoveAllLocks`，remove_lock）：原本的「全部」與「只第一個」顛倒，而且從物件本身開始找；改成只在內容裡找。
- 打壞門：直接打開再移除所有鎖，不經過解鎖（原本會觸發解鎖機關）。打壞箱子、木桶：移除第一個鎖，再當成沒有人使用的容器倒出（沒有訊息）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### C2-5 撿取與丟下（INTERACT.C、MISSILE.C ReturnObject）
- 地形阻擋（新增 `uimanager.BlockingTerrain`）：撿取與使用都要不被地形擋住：物件高度要在玩家可及範圍內；一般距離下，直線上不能有比玩家站的地方低的格子；路線不能跨到另一種地形（水、熔岩），除非有橋（0x164）撐著。UW1 比較整個地形值，UW2 只比類別。原本只看距離與高度差，隔著水或熔岩、在高處往下都拿得到。
- 撿取（player_3dget）的訊息：能撿但太遠是「太遠拿不到」（UW2 0x6A、UW1 0x5D），在範圍內但被擋是「你搆不到」（0x6B／0x5E），不能撿的不論遠近都是「你撿不起來」（0x6D／0x60）；UW2 的霜（0x1CA）在可及範圍內會被使用。
- 移動中的物件（例如飛行中的飛彈）不能撿。
- 重量檢查（EncumCheck）放在問數量之後、以選擇的數量判斷，而且只在真的要撿的時候（原本先以整疊判斷，拿一個也會說太重；不能撿的東西也說太重）。
- 拆一疊：從原本那疊複製資料（原本複製到自己，品質、擁有者、附魔都變成預設值），並清掉 next；拿走的部分也照樣通知擁有者。預設數量是 1。
- 擁有者的反應先於撿取機關（player_grabbed 再 release_3d）。
- 丟下：放置判斷改用 `motion.CanPlace`（原本另一份移植的判斷挑錯「站在上面」的物件、天花板也少算高度）；接在格子串列的最後。丟出的物件把品質存在移動物件的血量裡（落地時還原），原本存到 quality 結果落地後變成預設值。
- UW1 游泳時不降低可及高度（只有 UW2）。
- 未做的各項已在「C2-5 其餘」完成（預設模式的拖曳撿取在 C3-4）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### C3-1 人物欄、狀態點擊與選項選單（STATS、GAMESCR、WRAPPER、MAINMENU）
- 比對方式：5 個唯讀子代理分別比對 AUTOMAP、WRAPPER＋MAINMENU、STATS＋GAMESCR、PANELS＋RUNES、SCROLL／SCROLLIO／ICONS／INPUT，再逐條對照原版確認後修正。
- 指南針（`uimanager_compass.cs`，print_info）：
  - UW2 時段是 `÷ 0x1C2000 % 12`（原本 % 18，第二天起會印出不是時段的字串）。
  - 不再清掉訊息捲軸，報告接在原有訊息之後；對話中點擊沒有作用；只接受左右鍵。
  - 飢餓／疲勞那句與時段那句結尾加句號（UW1 的時段也是）；UW2 報告後空一行，沒有世界那一行時再空一行，讓報告的行數固定。
- 藥瓶（`uimanager_flasks.cs`）：中毒程度是 `(中毒 − 1) ÷ 3`（1～3 輕微、4～6 稍微…），原本用 % 5 會循環。
- 人物欄（`uimanager_stats.cs`）：UW1 沒有「技能點」那一列，第 i 列就是技能 i + 捲動位置，捲動 0～14（UW2 維持技能點列與 0～15）。
- 存檔前的檢查（check_save，`uimanager_options.cs`）：手上拿著東西或正在「用在…上」時不能存（UW2 0xAE、UW1 0xA0）；UW1 在 Ethereal Void（第 9 層）不能存（0x9F）。F1 開選項時也做同樣的檢查（deal_with_icons）。
- 選項面板：退出確認按「否」、存讀檔頁按「取消」會直接回到遊戲（原本回到選項第一頁），並清掉存檔清單；UW2 存檔時按 Escape 印「存檔已中止」（0xB4，UW1 維持「存檔失敗」）；讀檔成功印「讀檔完成」。
- 讀檔後一律收起武器（punt_fightmode），原本依存檔狀態拔著武器、進入攻擊模式。
- 未做：選項面板與主選單的鍵盤操作、Alt+X；UW2 頂層頁 Quit／Return 的位置；UW2 空白描述存成空格（已在 C5-2 完成）。沒有存檔時隱藏「繼續旅程」與自動播開場已在「C3-1 其餘」完成。
  - 已在後面的批次完成：Ctrl 快捷鍵、讀檔後「沒有圖示被選」（C3-4）；讀檔失敗的錯誤訊息、存檔清單標題（C5-2）。
- 驗證：建置 0 錯誤、單元測試 297 通過、1 略過。遊戲內未錄影。

### C3-2 符文袋、施法與作用中法術（RUNES.C、SPELLS.C、PLAYTIME.C dispel_spell、PANELS.C）
- 施法流程（`RunicMagic.CastRunicSpell`，player_cast）照原版順序：
  - 咒語失敗（技能檢定 0）：播失敗音效 0x16，印「咒語失敗」，不計冷卻。
  - 反噬：先印「法術反噬」再施放詛咒（原本順序相反），施放成功後播施法音效。
  - 施法音效改在法術真的施放之後才播（原本先播）；UW2 反噬時依原法術的小類別選音效。
  - 扣魔力的時機：施放前就扣。飛彈法術、以及 UW2 的指定目標法術（第 7 類）例外，要等瞄準放出才扣。
  - 法術被拒絕（do_spell 傳回 0：禁魔格、UW1 第 9 層、作用中法術已滿 3 個）時：已扣的魔力不退、保留待扣的作廢，播 0x16 並印「施法未成功」（UW2 0xE4、UW1 0xD5）。原本作用中法術已滿時不扣魔力。
  - `CastSpell` 改成傳回是否施放成功，不再自己扣魔力。原本法杖、卷軸等其他施法路徑若遇到咒語失敗後遺留的待扣魔力，會被誤扣。
  - 經驗不足、魔力不足、不列顛不能施第 4 環以上，三者共用 `FailSpell`（0xE1／0xD2 + 原因）。
  - 其他輸入模式中（手上有物件、「用在…上」、等待瞄準的法術）不能施法。
- UW2 指定目標法術（`CastClass7_SpellsOnCallBack` 改成傳回是否扣魔力，target_spells／obj_spells）：
  - 對生物的法術（0～7）只有處理函式成功才扣。
    - 流血、制裁敵人：對象要會流血。
    - 恐懼、魅惑、毒：對象是生物即可，被抵抗也算成功。
    - 麻痺：對象不能是不死生物。
    - 驅散不死：對象要是不死生物或飄浮骷髏。
    - 研究怪物：對象是生物、骷髏或鬼火。
  - 對物件的法術（8～15）一律扣，只有三種情況例外：驅散符文點到的不是符文、修補時沒有可修的東西、月門旅行點到的不是月石。
  - 流血與制裁敵人命中後更新眼睛（敵人血量）。
- 符文袋（mous_in_rune）：
  - 施法嘗試之後（含冷卻中被拒、不是法術），下一次左鍵點符文會先清空符文架。
  - 右鍵查看、左鍵加入，與互動模式無關（原本查看模式下左鍵也是查看）。
  - 其他輸入模式中點擊沒有作用。
- 作用中法術圖示（try_clear）：
  - 左鍵解除、右鍵說明，與互動模式無關；手上有物件或法術時沒有作用；空的格子沒有作用。
  - 「快要結束」的門檻是持續值 ≤ 2（原本 < 2）。
  - 解除懸浮或飛行時，會先變成持續 1 的緩降（原本保留原來的持續值）。
  - 解除法術（`playerdat.CancelEffect`）時，把最後一個法術移到空出的格子（原本把後面的往前移）。
- 生命／魔力瓶：改成整數的 `值 × 12 ÷ 最大值`，上限 12，最大值為 0 時顯示 0。原本用浮點數，最大值為 0 或數值超過最大值時會算出超出範圍的值。新增 `uimanager.FlaskLevel`。
- 測試：新增 `ActiveSpellPanelTests`（解除法術的格子移動、瓶子格數）。
- 眼睛動畫（PANELS.C adjust_eyes、COMBAT.C）：玩家打中生物時依 `3 − hp × 3 ÷ 平均血量`（最多扣到 2）分 1～3 級，每級 3 張圖；張開 1→2→3，完全張開停 16 拍，再 2→1，最後閉眼。動畫中又打中，回到完全張開重新停留。原本逐張往目標圖走，10 秒後才閉眼。每拍 0.1 秒是估計值。
- 捲軸／圖示／鍵盤那份報告的項目已在 C3-4～C3-6 處理，剩下的列在那幾節。
- 驗證：建置 0 錯誤、單元測試 304 通過、1 略過。遊戲內未錄影。

### C3-3 地圖（AUTOMAP.C、GRIDDB.C、DRAWOBJ.C、地圖碎片、hack trap 45）
- 地圖格子的高 4 位元改成兩個欄位分開讀（`automaptileinfo.Kind`／`Terrain`），原本是整個 nibble 比對固定值：
  - 種類（curautocode）：1 門、2 橋、3 樓梯貼圖。
  - 地形類別：1 水、2 熔岩、3 冰（只有 UW2）。
  - UW2 的種類在 bit 4-5、地形在 bit 6-7；UW1 相反。
  - 修正前 UW2 熔岩畫成一般地板、橋被當成熔岩，門在水上或冰上這類組合也認不出來。
- 寫入（`visionlos.cs`）：
  - 橋、門、樓梯只寫自己的種類位元（`KindBits`）。原本 UW2 的橋寫成 0x60、UW1 寫成 0x90，會把地形位元一起蓋掉。
  - 樓梯貼圖改成經過 `texture_map` 查關卡貼圖的地形，值是 3 或 4 才算。原本 UW1 永遠不成立，UW2 漏掉 4。
  - 橋要用自己的貼圖（flags < 2，原本 ≤ 2）。
  - 祕門也標成門（do_door 一律標記）；隱形的物件不標記（do_obj）。
  - 移除不再使用的 `TerrainDatLoader.GetTerrainDataBit012` 與 `automaptileinfo.GetDisplayType`。
- 繪製：橋與樓梯蓋過地形顏色（DoTile 的 kind 2／3 不上地形色）。
- 地圖區域法術（`automap.MarkCircleOfTilesVisited`，automap_area）：只記格子類型與地形類別，不記門、橋、樓梯；邊界照原版處理（x < 1 時移到第 1 欄，超過右、上、下邊界就放棄該列剩下的部分）。
- 迷路（UW2）：
  - 新增 `playerdat.map_scrap`（0xF9）。
  - 迷路時開地圖，顯示最後看過的那一層；map_scrap 的 bit 7 有設時，改開剛複製碎片的那一層。為了防止 map_scrap 是 0 的存檔出錯，這種情況顯示目前這層。
  - 關地圖時記下顯示的那一層（ExitAutoMap）。
  - 迷路時不畫玩家標記。
  - 恢復認路的條件：踩到低 4 位元是 1～9 的格子才算（player_newsq）。原本 0xA～0xF（知道但沒看見）也會恢復。
- UW1 在 Ethereal Void（第 9 層）不畫玩家標記、不給探索經驗。角色等級 0 不給探索經驗（原本條件是 ≥ 0）。
- 使用地圖：
  - UW2 印「你攤開地圖」（0x8E）。
  - 目前不是探索音樂（8～15）時，換成該世界的探索音樂。
- 地圖碎片（`a_bit_of_a_map.cs`）：
  - 筆記的 Y 座標換算改用 `(y − 7) ÷ 3`，原本是 −9。
  - map_section 的中央區改成距離平方 ≤ 0x51，原本是 <。
- hack trap 45（`automap.MakeTerrainUnseen`，make_terrain_unseen）：觸發點 (x..x+owner, y..y+owner)，兩端都包含。類型 0～5 改成 0xA～0xF，斜坡不變。
- 寶石：第 6、7 格（Loth 之墓與 Pits 對調的那兩格）的「已造訪」改用該格實際的世界判斷。原本兩者顛倒。
- 筆記：一層滿 100 則時點地圖不會開始寫。
- 測試：新增 `AutomapTileCodeTests`（兩代的欄位解讀、種類位元）。
- 未做：
  - UW1 樓層箭頭可翻到 99 層：已在「C3-3 其餘」完成。UW2 第 0x47 層（地圖碎片的第一格）用箭頭到不了，本專案的 UW2 地圖依世界分頁，不適用。
  - 細部畫法：已完成邊與門的規則（`automaprender.cs`，ShowDungeonMap／ShadeSide／DoDoorTile）：
    - 邊：鄰格是已見類型（1～9）就不畫邊，其餘（0、10、12～15）畫深邊；鄰格是 11（地圖碎片上沒走過的開放格）畫淡邊（`LightBorderColour`），原本完全不畫。
    - 斜角格：只畫斜線和開放那一半的兩邊，鄰格判斷同上。原本實心那一半的兩邊在鄰格開放時也畫線，開放那一半只在鄰格是 0 時畫。
    - 門：先畫中心點，再依南北、東西、兩個斜向的順序，找第一個兩側有一格「剛好是類型 1」（斜坡不算）的方向，畫一條橫過通道的線。原本先看東西、斜坡也算、沒有斜向，斜角格上的門也不畫。
    - 門只畫在已見類型（1～9）的格子上。
    - 逐像素畫法：已完成，`AutomapRender` 改寫成原版的畫法。在空白地圖畫面（BLNKMAP，UW2 BYT.ARK 第 0 張）的調色盤索引上，每格 3 像素，位置在 (x − 1)·3 + 10、(y − 1)·3 + 7（y 由畫面底部往上算）。照 DoTile 畫格子圖樣：地形色用 0xB1／0xB5（UW2 再加冰的 0xB9），橋用 0xE9 起的顏色，樓梯貼圖加暗，門照 DoDoorTile；再照 ShadeSide 畫邊，兩邊相接的角落也加暗。加暗一律是索引加上 base 與亂數（PixelDarken）。兩代的位元配置與 UW2 橋和樓梯不上地形色的規則都照原版。玩家標記改畫 BUTTONS.GR 的 0x3F 號圖（第 0 色透明），位置照 ShowAutoMapLevel。
    - 輸出是 320×200 的圖層，只含地圖改過的像素，疊在背景上，所以 HD 背景在其他地方照樣顯示；圖層用最近鄰取樣放大 4 倍。原本是 RGB 重新設計的畫法（每格 4 像素、自訂顏色），已移除，連同只有它在用的 `automaptileinfo.IsSolidWall`。
    - 測試：新增 `AutomapRenderTests`（格子位置、邊與鄰格的規則）。
    - 種類取最後畫的物件：已完成。原版的 curautocode 是一個全域值，格內每個物件畫出時覆寫，所以由最後畫的那個決定。do_objsort 由遠到近畫一格的物件（橋或門把其餘物件分成兩側，各自由遠到近），所以最後畫出的就是離視點最近的那個。本專案改成在格子串列裡符合條件的橋、樓梯貼圖、門之中，取離玩家最近的那個（`visionlos.cs`），原本固定以橋、樓梯、門的優先順序取。do_objsort 的視角座標表與分區排序沒有逐位元照抄，距離以玩家位置的直線距離代替。
  - 筆記寬度上限：已完成（起點 x 加上 FONT4X5P 的寬度最多 0x13B，兩代相同）。UW1 超出（寬度或 46 字）時的嗶聲：已完成，新增 `PcSpeaker.Beep`，以 300 Hz 方波播 10 毫秒（do_beep(0x12C, 0xA)）。
  - UW1 把筆記刪到一則不剩時原版不存檔（SaveTheWords 在 num_words 為 0 時直接返回），刪掉的筆記下次又會出現：會讓玩家困擾的原版 bug，不照做。
  - 開地圖時停掉音效（kill_all_effects）：UW2 已在 C6-3 完成；UW1 原版沒有。
  - 筆記座標偏移：已完成。原版的筆記和地圖格子用同一套畫面座標：string_to_screen(text, x, y)，y 由畫面底部往上算，是字的最上一列；新筆記的 note.x = 點擊的 x、note.y = 點擊的 y + 4。地圖改成原版畫法後，圖層正好疊在畫面座標上，所以筆記也照原版擺放（`RichTextLabelMapNote`）。字級 64，也就是 SysFontBuilder 的設計尺寸 16 放大 4 倍，和畫面一致；原本是 48，只放大 3 倍。字形頂端在標籤頂端下方 8 個設計單位（ascent 11，字高 4 列，descent 1），所以標籤放在 (x·4, (199 − y)·4 − 32)。新筆記照原版取點擊座標（`uimanager_map.cs`）。原本顯示在 x − 1、上緣 y − 2，建立時也偏了，結果和格子差 1～4 像素。DOS 存檔裡的筆記現在也顯示在原版的位置。
- 驗證：建置 0 錯誤、單元測試 311 通過、1 略過。遊戲內未錄影。

### C3-4 圖示與按鍵（INTERACT.C deal_with_icons、PLAYER.C 按鍵表、ICONS.C／OPTIONS.C do_option_shortcut）
- F1～F6 照各代圖示在畫面上的順序（`Controls.FunctionKeyIcon`）：
  - UW1：選項、交談、拿取、查看、攻擊、使用（原本就對）。
  - UW2：攻擊、使用、拿取、交談、查看、選項。原本兩代都用 UW1 的排法。
- F1～F6 在動作進行中（手上有物件、正在對某物使用、瞄準法術）一律無效。原本只擋 F1。
- Ctrl 快捷鍵（新增 `uimanager.OptionShortcut`）：
  - Ctrl+S 存檔、Ctrl+R 讀檔、Ctrl+M 音樂、Ctrl+F 音效、Ctrl+D 細節、Ctrl+Q 離開。打開選項面板並按下頂層對應的按鈕。
  - 動作進行中按下會拒絕，印 UW2 0xAE／UW1 0xA0。
  - 頂層按鈕的處理抽成 `PressMainOptionButton`。
- 預設模式（RightButtonThing 0）：兩代 classic 開局、讀檔都不選圖示，原本是使用模式。
  - 3D 視窗點一下等於查看；按住拖動超過原版畫面 6 像素就拿起（player_3dlook 的 mouse_dragged）。原本 classic 沒選圖示時點了沒有反應。
  - 正在對某物使用時，點擊照常是使用。
  - 從人物欄收起武器（punt_fightmode）一樣回到沒有選圖示，並換回漫遊音樂；原本 classic 會回到使用模式。
- 攻擊圖示（deal_with_icons）：
  - 一定拔出武器，原本是切換，已拔出時反而收起。
  - 游泳時（motion_state bit 0）拒絕；UW2 另外在睡夢虛空中或麻痺時拒絕，回到沒有選圖示。
  - 音樂：目前不是戰鬥主題（UW1 5～7、UW2 2～4）才換成拔刀主題；收刀時只有目前是拔刀主題才換回漫遊音樂。原本兩者都會強制切換。
- 游標：使用、查看、拿取模式改用 cursors.gr 第 11 號（force_mouse_cursor）。`SetCursorToCursor()` 不給編號時跟著目前的模式。
- `'` 作弊鍵（60 法力、全部符文、16 級）只在除錯版有效。原版這個鍵沒有作用。
- 測試：新增 `FunctionKeyTests`。
- 未做：
  - 人物欄拔刀（pick_fightmode）：已在「C3-4 其餘」完成。
  - 查看模式（RightButtonThing 3）的找陷阱提問：已在「LOOK.C 查看與鑑定」完成。
  - UW2 音樂、音效快捷鍵的 music_opt／sound_opt 只是選定接下來開關音樂還是音效；本專案按頂層的對應按鈕時做同樣的選擇，結果一致。
- 驗證：建置 0 錯誤、單元測試 327 通過、1 略過。遊戲內未錄影。

### C3-5 classic 的移動（PLAYMOVE.C parse_playin／do_player_keyboard、PHYSICS.C simple_fizix、PLAYER.C chg_plyp、INTERACT.C mous_in_3d）
- 使用者決定：classic 照原版，modern 保留原本的預設鍵（`Controls.ClassicKeys`／`ModernKeys` 分開）。
- 按鍵（`ClassicKeyboardMotion`）：
  - W 跑（0x70）、S 慢走（0x32，新增 `Action.Walk`）、X 後退、A／D 轉向、Z／C 側移、E／Q 升降（只在飛行或漂浮時；否則停下）。
  - 照原版的順序處理，後面的鍵蓋過前面的。
  - 按著 Shift、Alt 或 Ctrl 時連續移動不作用。
  - 修正前是 S 後退、A／D 側移、Q／E 轉向、R／F 升降、Shift＋W 慢走。
- 單步鍵：大寫 A／D 轉 45 度，不在八方位上時先轉到最近的一格；兩次之間至少隔 0x18 tick。
- 俯仰：1／3 每按一次（含按住的自動重複）改 0x400，上下各最多 0x1000，2 回正。原本是按住後每個物理 tick 都改。
- 滑鼠（`ClassicMouseMotion`）：
  - 在 3D 視窗按住左鍵移動。最下 1/5 依左中右三等分，分別是左移、後退、右移；其餘部分依指標離中間 1/3 的距離轉向（最多 0x80），依高於 2/5 的高度決定前進速度。
  - 雙鍵同按是跳；在空中或游泳時改成前進；武器拔出時右鍵照常攻擊。
  - classic 的左鍵不再互動，互動只用右鍵。雙鍵同按時（攻擊模式除外）右鍵不互動。
- 測試：新增 `ClassicKeysTests`（classic 是原版鍵、modern 預設不變）。
- 未做：
  - 大寫 W／S／X 的單步前進、後退：已在「C3-5 其餘：單步移動」完成。
  - CapsLock：已完成（`main.TrackCapsLock`）。原版「Shift 和 CapsLock 剛好一個有效」時持續移動不作用，大寫字母是單步鍵。Godot 讀不到鎖定狀態，改從按鍵推算：字母打出大寫，就表示兩者剛好一個有效，所以每按一個字母都重新校正；按 CapsLock 鍵則切換。持續移動與單步鍵都改用這個判斷。開遊戲後第一次按字母之前，假設 CapsLock 沒開。
  - 3D 視窗上方三個可以點的箭頭區（單步）：已在「C3-5 其餘：單步移動」完成。
  - 按住左鍵時把指標限制在視窗內（mouse_constrain）：已在「C3-5 其餘：遙視與指標限制」完成。
  - 遙視（Roaming Sight）時的滑鼠移動（move_cam(0)）：已在「C3-5 其餘：遙視與指標限制」完成。
  - 單步轉向的「不在下落中、速度低於 MaxPlayerAccel」：已完成。MaxPlayerAccel 就是本專案的 `MotionWeightRelated_dseg_67d6_C8`（0x60 減去負重的影響），玩家速度是 `momentum_14`，兩個條件都檢查。
  - MOUSE.C 的鍵盤游標（Tab、方向鍵）、INS／DEL 當滑鼠鍵、左撇子對調、Alt+H、Alt+X：已完成（`main.KeyboardMouse`）。
- 驗證：建置 0 錯誤、單元測試 342 通過、1 略過。遊戲內未錄影。

### C3-6 訊息捲軸（SCROLL.C scroll_print3、SCROLLIO.C scroll_wait／scroll_more／wdialog／wyorn、INVPANEL.C AskHowMany）
- [MORE]：
  - 只有「新的」按鍵或點擊會結束。原本放開按鍵、按住的自動重複、只按 Shift 都會結束，例如放開 W 就跳過了。
  - UW2 的 [MORE] 用 0x12 號色（原本用 \3＝0x02）。UW1 的 0x60 剛好是 \3，不變。
  - 出現時機（`AddText`）：一則訊息先印滿 Rows 行（UW1／UW2 下方捲軸 5 行），之後每次 [MORE] 再印 Rows−1 行。原本每印 4 行就停。\m 之後重新計算。
- 跳脫碼（`AddText`）：
  - \0～\6 從所在的那一行起換顏色。原本只處理 \m，其他碼都照字面印出來；例如存讀檔訊息的 \4、\6，以及對話裡的 \p。
  - \p 停 600 tick、\P 停 200 tick，有按鍵或點擊會提早結束，之後的字接在同一行。
- 是／否提問（trapdisarming、repair）：
  - 只有 Y／N 會改答案，其他鍵不理會（原本任何鍵都變成「否」）。
  - 點滑鼠會結束提問：只按右鍵是「是」，左鍵或雙鍵是「否」（呼叫端的 `wd_bool(r == 2)`）。
  - 提問和答案接在原本的訊息後面並保留在捲軸上（`MessageDisplay.FreezeTypedInput`）。原本提問前後都會清空捲軸。
- 輸入一行（wdialog：拿取數量、祭壇咒語）：
  - 點滑鼠會結束輸入。數量提問（`MessageDisplay.QuantityPrompt`）左鍵是 1 個、右鍵是整疊、雙鍵是取消，答案改寫在原處（wd_replace）。
  - Esc 顯示「-」、不給答案。
  - 輸入前後都不清空捲軸。對話中的物品欄數量提問仍然先清掉，結束後再放回答案選單。
- 沒有單元測試：捲軸的繪製與輸入都依賴 Godot 的控制項。
- 未做：
  - 最後一段文字可多用一行（scroll_print3 的 flag 0）：已完成。`MessageDisplay.FinalPieceStart` 照原版切出最後一段（每 49 字在最後一個空白切塊、反斜線之前、換行之後），從最後一段開始的那一行，在 [MORE] 前可以多捲一行（每次 [MORE] 之後也一樣）。測試：`MessageScrollPieceTests`。
  - menus_active：已完成。對話的答案選單顯示中，有別的訊息印到下方捲軸（例如查看交易欄的物品）時先清空捲軸再印。選單等 500 tick（或一次按鍵、點擊，這次輸入不算作答）後清空並重印答案（converse_event_loop）。等待期間不接受作答（`ConversationVM.MenusActive`、`WaitForMenuAnswer`）。數量提問仍沿用先清掉、之後放回的做法。
  - 畫面模式不是 1／4 時丟掉訊息：已完成（`AddToMessageScroll`）。只在遊戲（含選項面板）與對話中印字，地圖、過場、主選單、建角時丟掉。逐一確認過會在模式切換前後印訊息的地方：攤開地圖先印「你攤開地圖」才切到地圖；過場結束先還原模式才跑回呼；新遊戲與讀檔都先經過 `JourneyOnwards` 設成遊戲模式；選項面板裡的存讀檔訊息照常印。都和原版的順序一致，不會丟掉該顯示的訊息。
  - 輸入一行的編輯鍵、預設文字被取代、只接受數字：已在「C3-6 其餘」完成。
  - 鍵盤游標與 INS／DEL（MOUSE.C）：已完成（`main.KeyboardMouse`）。
- 驗證：建置 0 錯誤、單元測試 342 通過、1 略過。遊戲內未錄影。

### C4-1 過場腳本規則（CUTS.C 兩代、runcutscene／show_cutscene、RectLook 墓碑）
- 先掃過兩代所有 `.n00` 腳本的 opcode 用法（scratchpad `cuts/scan.py`）。原本 TODO 寫的「調色盤複製（16）、計時回呼（17）」其實是 palrange、palfade，兩代都沒有腳本用到。
- 解析（`CutsceneCommand.cs`、`cutsutil.cs`）：
  - palrange、palfade 的長度改由自己的數量參數決定（3 個字一個顏色）。原本當成 0 個參數，若出現會讓後面全部錯位。
  - opcode 名稱與說明改成原版的意義。例如 4 是 skip、11 是 jump、12 是 punt、24 是 splity（原本寫成音訊設定）。
- 輸入（新增 `cutsplayer.InputReceived`，由 `uimanager._Input` 呼叫）：
  - Escape 以外的按鍵或滑鼠左右鍵會結束 pause；在 skip 範圍內會跳到目標格，並取消 loop。原本只有 Escape 有作用，而 Escape 會結束整段過場。所以香爐異象、墓碑這類 pause 999 的畫面，只能按 Escape 離開。
  - UW1：說話時不接受輸入；滑鼠鍵與上一次輸入（或檔案開頭）要隔 0x40 tick（1/4 秒）；淡入之前、淡出之後不能 skip（fade47 == −2）。
  - punt（12）為 0 時 Escape 無效。UW2 的結局（過場 2）不能用 Escape 跳過。
- 時間（時鐘是 1/256 秒，`>> 8` 就是秒）：
  - pause 改成 arg 秒，999 為等到有輸入。原本是 arg/2 秒，而沒有 frame-set 的段落還會再多等 arg/2 秒。
  - wait（14）：UW1 說話中會等到說完，再等 arg[1] 秒，期間不接受輸入；UW2 等 arg[1] 秒，期間不接受輸入。原本有 frame-set 的段落完全忽略 wait。
  - wait_for_sound（27）：最多等 arg 秒（原本 arg/2），時間到就切掉語音。
- 影格控制：
  - jump（11）不畫中間的影格。skip 或 jump 跳過的影格照常推進，但不顯示、不等待，期間 txt、pause、wait、skip 都不作用。
  - loop（7）重播到該格 arg 次，重播時不再執行已經跑過的指令。原本不處理，所以 UW1／UW2 死亡骷髏（cs403）、UW1 標題都只播一次。
- 字幕：
  - erase（1）清掉字幕（原本不處理）。UW2 txt 的 FFFFh 清掉字幕。
  - say（13）的 999（只有文字）與 UW2 的 998（沒有語音）照影格立刻換字幕。原本每個 say 都要等前一段語音播完才換。所以 UW2 守護者的夢（cs030，一段 15 秒的語音配 4 行字幕）後 3 行要等語音結束才一起閃過。
  - 有語音的 say 仍然等前一段說完才開始：原版在別段語音播放時不會開始新的語音，新的那段會遺失。
  - 本專案一律顯示字幕，原版有語音時不顯示，exhumed 的 port 也把字幕列為可選的增強功能。
- 小視窗過場（≥ 100h）：
  - 淡入、淡出只作用在全螢幕。小視窗一開始就是正常亮度，原本先調成黑色。
  - 過場結束時停止語音（show_anm 結束會切掉語音）。
- 開頭配樂：UW1 過場 1～3 換成主題 4，UW2 結局（過場 2）換成主題 1（load_new_music）。
- 墓碑（`gravestone.cs`，RectLook）：
  - 編號改成 quantity 物件取 link & 0x1FF、否則取 owner，再到 GRAVE.DAT 查圖號；讀不到就什麼都不印。
  - 圖號 0 時先印「墓碑上寫著：」（字串 8/0x160＋flags），且不顯示圖。原本一律顯示第「圖號−1」張，圖號 0 時變成 −1。
  - UW2 沒有墓碑圖（player_look_grave 是空函式）。
- 測試：新增 `CutsceneScriptTests`：palrange／palfade 的長度、UW1 next 多一個字，以及兩代所有腳本解析後的 opcode、影格都在範圍內且有 end。
- 未做：
  - 播放引擎：已在「C4-1 其餘：播放流程」完成。本專案仍先依 next／end 把腳本切成每個檔案一段，再逐格播放；對照原版 show_anm 的逐筆流程，每一格的指令時機、段落長度、每格時間都已一致（細節見該節）。
  - 淡入淡出、palsimplefade 的時間推進：已在「C4-1 其餘」完成。
  - 沒有語音時 skip 加的 pause：不適用。原版的「有語音」（flags.b5）來自 speech_available()，也就是 speechok：有沒有載入數位音效驅動（音效卡），與有沒有語音檔、遊戲內的音效開關都無關。本專案永遠有數位聲道，等於有音效卡的原版，所以 b5 = 0 的路徑（skip 加停頓、wait 用第一個參數、say 改成字幕）不會出現。
  - UW2 vscreen、focus、pan、splity：已在「C4-1 其餘」完成。
  - 小視窗的位置：已完成（`cutsplayer.PlaceSmallWindow`）。UW1 是 (0x34, 0xB4, 0xAC×0x70)，UW2 是 (0x10, 0xB6, 0xD0×0x7F)；死亡過場 103h 在 UW1 再上移 9 列、加高 0xB 列，UW2 上移 4 列、加高 5 列。show_anm 在視窗裡畫影格最後的 w 欄、h 列，所以整張 320×200 影格放在視窗左上方 (320−w, 200−h) 處。原本控制項固定在 (−96, −67)：水平剛好一致，垂直 UW1 差 2 列、UW2 差 11 列。墓碑、豎井的圖也用同一個位置。控制項在執行時放進一個視窗大小、會裁切的父控制項（CutsSmallClip），只顯示視窗內的部分，與原版相同；原本整張影格都畫出來。
  - 開始時停掉數位音效（punt_all_digi_fx）：已在「C4-1 其餘」完成。
  - 墓碑的三個條件：已在「C4-1 其餘」完成。
  - palrange、palfade：已在「C4-1 其餘」完成（兩代都沒有腳本用到）。
- 驗證：建置 0 錯誤、單元測試 317 通過、1 略過。遊戲內未錄影。

### C4-1 其餘：淡入淡出、開頭停音效、墓碑與豎井（GRFX.C fadein／fadeout、CUTS.C show_cutscene、LOOK.C RectLook）
- 淡入淡出（`cutsplayer.Fade`）：fadein／fadeout(n) 分 n×8 步、每步 8 tick（1/32 秒），共 n/4 秒，亮度線性變化；n 為 0 時立刻切換。原本是 2/n 秒（n 越大越快），而有 frame-set 的段落完全是瞬間切換。
  - 淡入淡出會擋住播放（原版忙等），兩種段落都一樣。
  - 淡入過之後再淡入、淡出過之後再淡出都不作用（fade47／fade49 的 −2）。
  - 期間收到的輸入丟掉（gobble_input_events）；跳格中立刻切換。
- palsimplefade（19）：照 install_timebased_task／run_timebased_tasks 依時鐘推進：每 period×8 tick 前進一步，共 total 步，最後一步就是目標色（palette_fade：起點＋步數×差距÷total）。原本每畫一格前進一步，總步數又除以 period，速度隨畫面更新率改變。原版的時間工作在 pause、wait、說話時也照樣推進（run_time_critical_things 即時改 DAC），所以停在同一格時顏色也會變；本專案在這些等待迴圈裡也用當下的漸變調色盤重畫這一格（`RefreshPaletteFade`）。原版在 Transparency 為 1 時從第 1 色開始漸變；Transparency 是畫面板與游標時暫時打開、畫完就清掉的繪圖旗標（PANELS.C、MOUSE.C），過場執行時是 0，所以實際上一律從第 0 色開始，與本專案相同。
- splity（24）與 vscreen 的第三個參數（20）：寫到同一個 `splitY`（原版的全域 splity，跨過場保留）。影格只畫上面 200 −（splity + 1）列，下面是字幕列，字幕放在字幕列裡。原本 24 被當成「音訊設定」什麼都不做，只有全景模式才調字幕位置；所以 CS030 守護者之夢、CS004～007 這些只用 24 的過場，畫面沒有裁切，字幕也不在字幕列裡。兩代腳本用到的值：40（CS000、CS030～036、CS040）、4（CS004～007、CS034）。
- 字幕位置（cuts_draw_text）：原版把字幕由下往上畫在 fh × 行數 + 1，所以最後一行的底部一律在畫面底部上方 2 列，和有沒有字幕列無關（字幕列只負責清成黑底）。本專案原本把字幕從字幕列頂端往下排，單行字幕會浮在字幕列上半部。改成全螢幕過場的字幕靠底對齊：文字排好後（`Finished`），依內容高度把標籤移到底部上方 2 列。Godot 4.3 的 RichTextLabel 沒有垂直對齊屬性。小視窗過場不動。實機截圖確認 UW2 CS030 與 UW1 開場（CS001）。
- 垂直捲動（vscreen_focus、do_pan）：畫面頂端是虛擬畫面的第 h −（focus_y + 1）列，捲動量是 (frame + 1)·step。原本用 (focus_y + 1) % h，而且少一格。CS000（focus 359）結果相同；CS002（focus 199）的起點從第 200 列改成第 160 列。水平捲動原本就一致。
- palrange（16）：把腳本給的顏色（每色三個 6 位元的字）寫進影格調色盤的一段。palfade（17）：照 palsimplefade 的時間推進方式（period、total），把那一段顏色漸變到腳本給的顏色。兩代都沒有腳本用到，照原版補上。
- UW2 過場開始時停掉數位音效（show_cutscene 的 punt_all_digi_fx）；UW1 的 runcutscene 沒有。
- 墓碑（`gravestone.cs`）：
  - 有圖時先清空訊息捲軸。
  - UW2 編號 0 的墓碑：從墓碑本身、它的內容物、同串列後面的物件裡找 6/2/3 的觸發器（find_obj），有就不印碑文。
- UW1 豎井（`tmap.cs`）：改看該貼圖的地形是不是 TERR_WINDOW（9），原本寫死貼圖 142。UW1 的貼圖 127 地形也是 9，所以也會顯示豎井畫面。
- 驗證：建置 0 錯誤、單元測試 377 通過、1 略過。遊戲內未錄影。

### C6-2 其餘：數位音效播放中重新定位（SOUND.C update_digi_playback、play_effect、play_effect_here）
- 新增 `DigitalEffectPlayback`：UW2 的數位音效改成用 AudioStreamGenerator 一點一點混進去，大約 0.2 秒的緩衝（原版 AIL 是兩個 2KB 緩衝輪流補）。每格畫面補緩衝時，用當下的音量與左右聲道計算增益（`StereoPanBake.Gains`，照 AIL 的 pan_graph 公式）。原本播放前就把左右與音量寫死在取樣裡。
- 從某個位置播放的音效（`PlaySoundEffectAtCoordinate`，play_effect 的 digi_src type 1）記住音源。每格依玩家當下的位置與朝向重算音量與左右（sound_move）；太遠時音量變 0，但仍繼續播放。play_effect_here 與 Guardian 的笑聲（≥ 100）沒有音源，不重算。
- 播完、被 kill_effect 停掉，或播放器換成過場語音時，就放開音源。
- 原版還有跟著移動物件的 type 2（play_effect_on_mobile_src）；本專案沒有對應的呼叫者，沒有做。
- 移除不再使用的 `UWsoundeffects.Int16ToBytesLE`。
- 循環（digi_fx_play）：SOUNDS.DAT 長度 ≥ 5000 的音效（不含 Guardian 的 ≥ 100），播完後再重播 (length >> 6) − 1 次。照原版等緩衝播空了才重播。
- 快取（ds_sounds_in_ems）：最多 4 個解碼後的取樣，再播時不再讀 VOC，滿了換掉最久沒播的。原版的 EMS 頁不夠時會整場關掉數位音效，那是 DOS 的記憶體限制，不照做。
- 驗證：建置 0 錯誤、單元測試 377＋90 通過。聲音要遊戲內聽過確認。

### C5-2 其餘：ObjCrunch（OBJECTS.C ObjCrunch／Obj_ListOkay、MAP.C Map_Save）
- `LevArkWriter.CrunchObjects`：UW2 寫出每一層之前，先檢查物件串列：
  - 兩個空閒串列沒有重複的編號。
  - 從每格的串列、每個物件的內容物走得到的物件不重複；同一串走到超過 10 次就當成迴圈。
  - 空閒數加上走得到的數量，移動物件剛好 0xFF、靜態物件剛好 0x300。
  - 玩家（1 號）不在任何格子上時另外算一次（GrSq < 0；不是目前這層的區塊就是這樣）。
- 一致時，把空閒的記錄清成 0：移動物件整筆 0x1B 位元組、靜態物件前 8 位元組。只改寫出的副本，記憶體裡的關卡不變。
- 不一致時照原版問玩家（字串 0x96「物件串列損壞……要繼續嗎？」，預設「否」）：存檔前用 `LevArkWriter.UW2ObjectListsOkay` 檢查本次載入過的每一層，答「否」就不存、顯示存檔失敗；答「是」就照存，但不一致的那層不清零，因為空閒串列上可能有使用中的物件，清掉會讓存檔少東西（原版不一致時 ObjCrunch 本來就不清零）。存檔時手上拿著物件不准存，所以原版另外計入的游標物件不會出現。
- 測試：新增 `ObjCrunchTests`。原版第 1 層一致，空閒記錄會清零；把格子上的物件塞進空閒串列後就不清零，區塊也不變（379 通過、1 略過）。

### C4-1 其餘：播放流程（CUTS.C show_anm 的逐筆迴圈、cutsop_next／cutsop_end）
- 原版逐筆播放 LPF 的影格記錄。每畫一格就把格數加一，跑這一格的腳本指令，再等到距上一格滿 0x100/rate tick。next（5）與 end（6）讓這個檔案在那一格停下，下一格的指令照樣會跑；記錄播完（回到開頭的差異格不算）檔案也就結束，超過的指令永遠不會執行。
- 本專案的段落播放本來就是「跑指令→畫格→等待」的順序，和原版一致。改的是三處：
  - 段落長度以 LPF 的記錄數為上限。原本 next 超過記錄數時會繞回第 0 格繼續播。全景捲動的段落不受影響，它的影格來自合成圖。
  - end 一律在它那一格停下。原本只有「之前沒有任何 frame-set」時才這樣；否則改用舊的估計做法，播到最大指令格數為止。
  - 每格時間用原版的整數 tick：(0x100 ÷ rate) ÷ 256 秒，原本是 1/rate 秒。例如 rate 14 是 18 tick（0.0703 秒），原本 0.0714 秒。
- 用兩代所有腳本實測（照原版的換檔規則：data（8）指定的是「下一個」檔案，副檔編號是八進位，每開一檔加一）：
  - 每一個 next／end 都沒有超過那個檔案的記錄數，所以上限在出貨的資料上不會改變行為；「指令跑不到、繼續播下一個檔案」那條路不會發生。
  - data（8）都放在段落結束的那一格，本專案立刻換檔和原版「下一個檔案」的結果相同。例外有兩組：UW1 夢境（CS030～041）在第 0 格指定第一個檔案，原版在開檔前先跑第 0 格的指令；UW2 的 997 格（996 隨機的 Guardian 檔），原版在開檔前跑 0x3E5 格的指令。本專案在第 0 格先跑指令再顯示，並對 996 有專門處理，兩組結果都相同。
- 驗證：建置 0 錯誤、單元測試 380 通過、1 略過。遊戲內未錄影。

### C5-1 換層（GAMEWRAP.C ChangeLevel／do_level_hacks、UWEDIT.C new_player_pos、INVSAVE.C、MAINMENU.C）
- 手上（游標上）的物件會跟著玩家換層（INVSAVE.C 把游標物件連同裡面的東西和物品欄一起存放）。原本會丟在原層玩家腳下。
  - 夢境仍然維持丟在腳下，避免把東西帶出夢境，與原版不同。
  - 作法：先移進物品欄資料，載入新層後再放回游標，重量不變。
- 順序：離開這層的特例（do_level_hacks mode 1）改在拿走玩家、存放手上物件之前執行。原版是在 SaveLevel 之前，所以 Academy 收回念力魔杖時找得到游標上的魔杖。
- ChangeLevel：換層時結束正在進行的攻擊（clear_fight_state），放開正要對某物使用的物件（GameInputMode 2）。
- UW2 各層特例：
  - 抵達時沒有拔刀就換成該世界的漫遊音樂（set_random_walking_music(-2)）。原本是 TODO。
  - Killorn 墜毀（quest 50）只在離開時檢查。原本「不是抵達」就算，也包括讀檔。
  - 原版 switch 從 Killorn 直接落到 Academy 那個 case，所以離開 Killorn 第 3 層（第 19 層）也會收回念力魔杖。
  - 抵達後 restore_mana(0)：魔力不超過上限（Academy 不能回魔的樓層除外）。
- 開局與讀檔（新增 `Teleportation.LevelEventsOnGameStart`）：
  - 新遊戲照 real_start 執行抵達第 1 層的特例（mode 0）。UW2 包含打開出生點的門、漫遊音樂。
  - 讀檔執行 mode 3：UW1 在以太虛空讀檔時關閉自動地圖。原本 mode 3 從來沒有執行，常數也誤寫成 2。
- 已確認一致：Prison Tower 的警報與第 2 層的兩個觸發、Tomb 的 genocide、以太虛空的地圖開關、UW1 Tybal 的魔力與地圖、Armageddon、抵達時清除最後擊中的生物與路徑、離開時讓生物「經過時間」。
- 未做：
  - npp_func 的順序：已在「C5-1 其餘」完成。找不到落腳處時原版玩家死亡、不放置，本專案仍落在目標格（刻意差異）。
  - 換層的淡出淡入：已完成（`Teleportation.Fade3D`）。fadeout3d／fadein3d 不管參數，固定 13 步（out3d／in3d 的 0～12），每步用光照表的一列把 3D 畫面變暗（cDimFB），一步一個畫面，最後填成黑色；淡入反過來。本專案在有傳送待處理、NewPlyFade bit 0 有設時，先讓 3D 視窗用 13 個畫面逐步變暗（期間 `blockinput` 停住遊戲），才真正傳送；bit 1 有設時，傳送後再用 13 個畫面淡回來。原本只是一閃的黑色遮罩。3D 視窗輸出的是顏色而不是調色盤索引，所以每一步用該列光照表讓遊戲調色盤平均剩下的亮度（`LightRowBrightness`，由 LIGHT.DAT 算出）：UW1 從 1.0 到第 12 步的 0.33，UW2 到 0.17，接著才變黑。
  - UW2 每次換層重新載入音效（load_digi_fx）：不適用，那是 DOS 在 EMS 裡重新快取數位音效；本專案的快取見 C6-2 其餘。
- 驗證：建置 0 錯誤、單元測試 342 通過、1 略過。換層與讀檔需要遊戲內確認，沒有單元測試。

### C5-1 其餘：npp_func 與復活（UWEDIT.C new_player_pos、SKILLS.C do_resurrect／do_gem／do_mstone、WORLDEV.C put_player_in_jail）
- 新增 `Teleportation.CodeToRunBeforeLanding`（npp_func）：新層載入、抵達特例跑完之後，找落腳格之前執行，可以改目標格（`Teleportation.MoveTo`，moveto）。到達後的音樂和訊息仍放在最後執行的 `CodeToRunOnTeleport`，因為抵達特例會換漫遊音樂，原版的 npp_func 在它之後才換。
- 新增 `Teleportation.NewPlyFade`：死亡復活設成 0，不再變暗一次（原版 player_is_dead 已經淡出過）；每次傳送後回到 3。
- 月石（UW1 Gate Travel、UW2 月石）：改成 npp_func 把目標移到月石所在格，再照常找落腳格（do_mstone）。原本先落在 (32, 32)，回呼裡直接把玩家放到月石格，沒有找落腳格。
- UW1 銀樹復活：一次傳送，npp_func 把目標移到銀樹（do_resurrect），再給復活的血量與魔力，最後換音樂 4。原本先落在 (32, 32)，回呼裡再傳送一次到樹。刪除 `TeleportToSilverTree`。
- UW2 黑岩寶石復活（do_gem）：
  - 目標改成 (0x1C, 0x28)，再沿隨機方向移 8 格（新增 `Teleportation.MoveAlong`，move_along），原本固定 (28, 37)。
  - 血量是復活的「最大值少 2～4」，原本回呼裡又補滿。
  - 血跡留在抵達的那格（開放格才留，細位置隨機 1～5），原本留在死亡的地方。
  - 最後換音樂 10、印 0x169。
- UW2 入獄（put_player_in_jail）：不跑復活效果（不回魔、不解毒、不留血跡），血量設成力量值。原本先跑復活效果，再把血量補滿。
- 已確認：UW2 夢境醒來（do_dreamret）的位置與朝向、競技場（set_me_inarena）放在回呼裡，結果相同。
- 入獄的牢門與 Lord British：已完成。
  - 抵達格 (0x2A, 0x26) 的門照原版解鎖並打開（gruesome_door_hack，抽成共用的 `Teleportation.GruesomeDoorHack`，抵達第 1 層也用它）。原本是關上。
  - Lord British 移到 (0x2A, 0x22)（teleport_critter），原本 (40, 35)。
  - owner 設成 0x27；原本寫成 38，與旁邊的註解 0x27 不符。
- 測試：新增 `MoveAlongTests`（377 通過、1 略過）。遊戲內未錄影。

### C5-1 其餘：夢境中拿著的物件（SKILLS.C go_void／punt_void／punt_mouse_obj、INVSAVE.C）
- 原版：進入夢境（go_void）時不放下游標上的物件，換層時照 INVSAVE.C 帶著走；醒來（punt_void）時先用 punt_mouse_obj，把它放在玩家附近（near_mob_put_at 範圍 6），所以物件留在夢境那一層。
- 本專案原本在 `HandleTeleportation` 裡，只要 `DreamingInVoid` 就把物件丟在腳下。但醒來時 `AwakenFromTheVoid` 先清掉了 `DreamingInVoid`，所以夢境中的傳送會丟，醒來反而帶出去，和原版相反。
- 改成：傳送一律帶著游標上的物件；醒來時先呼叫 `playerdat.PuntMouseObject`（從死亡處理抽出的 punt_mouse_obj），把物件放在夢境裡。
- 驗證：建置 0 錯誤、單元測試 382 通過、1 略過。遊戲內未錄影。

### C5-2 存讀檔（GAMEWRAP.C SaveGame／RestoreGame／DoSaveRest、AUTOMAP.C SaveAutoMapLevel、UWEDIT.C reset_game）
- 比對由唯讀子代理完成，逐項對照原版後修正。
- UW2 自動地圖探索紀錄：
  - `LevArkWriter` 把本次載入過的每一層自動地圖寫回區塊 160＋層（原版的 0x9F＋層，從 1 起算）。原本 UW2 只寫地圖和筆記，讀檔後從載入以來新看過的格子全部消失。UW1 原本就有寫。
  - 新增 `Uw2AutomapSaveTests`。
- 讀檔後的狀態（`Teleportation.LevelEventsOnGameStart` 讀檔分支）：
  - UW1 自動地圖開關重設為開，再由 mode 3 在以太虛空關掉（reset_game）。
  - 物品施法的冷卻歸零（nextSpellTime，`MagicEnchantment.ResetSpellTimer`）。
  - 清除最後擊中的生物與生物的路徑旗標 B15_7（GetLevel 的 init_level_creature_stuff）。原本只有抵達新層時才清。
  - UW2 清除畫面震動的位元 0x20／0x40／0x80（reset_game 把長度歸零，parse_effect 隨即清位元）。UW1 維持原本的短暫震動。
- 讀檔訊息與判斷（`uimanager_options.cs`）：
  - 槽位有沒有存檔看 DESC（get_save_descs），原本看 LEV.ARK。
  - 成功時印「讀取遊戲 」＋4 個「……」（UW2 0xB5、0xB9／UW1 0xA6、0xAA），再印「讀取遊戲完成」。
  - 有 DESC 但缺 LEV.ARK 或 PLAYER.DAT 時印「讀取遊戲失敗」（0xB1／0xA3）。原本沒有失敗分支。
- 存讀檔清單加上標題（ShowSaveRest：UW2 字串 0x120，UW1 寫在程式裡的「Save Game Descriptions」）。
- UW2 不能存檔時（動作進行中）關閉選項面板（save_opt 的 gameopts_done）。UW1 仍停在頂層。
- `docs/save-architecture.md` 的已知限制更新：兩代都有存自動地圖。
- 已確認一致：
  - PLAYER.DAT 的格式、加密、物件筆數與游標物件位置。
  - LEV.ARK 區塊編號、各訊息字串編號、存檔名稱長度與預填。
  - 讀檔順序（先讀玩家資料再讀關卡）、收刀與預設模式。
- 未做：
  - UW2 空白存檔名稱：已完成。UW2 的 SaveGame 在描述為空時寫成一個空白（GAMEWRAP.C 第 281 行）；UW1 的 SaveGame 沒有這段，照原樣寫出 0 位元組。原本兩代都寫 0 位元組，測試註解說是「兩代實測」；反編譯是逐位元組對得上執行檔的，所以 UW2 照反編譯（`SaveDescription.Encode`，新增 UW2 的測試）。
  - 只影響 DOS 讀本專案存檔的部分（都已完成）：
    - UW1 動畫覆蓋層的結尾標記。
    - UW2 計時器清單超出 timercount 的部分清零。
    - ObjCrunch：已在「C5-2 其餘：ObjCrunch」完成；0x96 提問改為照存。
    - 區塊的壓縮旗標。
  - lstime、nextstep、watertime：已完成。它們不是存檔欄位，而是 reset_game 的 reset_times 在開新遊戲與讀檔時清零的計時。對應本專案的符文施法冷卻（`RunicMagic.ResetSpellCastInterval`）、下一步腳步聲與水聲重播（`playerdat.ResetFootStepTimers`），連同物品施法冷卻（nextSpellTime），改在 `LevelEventsOnGameStart` 的兩種情況都清零。原本只在讀檔時清物品施法冷卻，其他三個沿用上一局的值。
- 驗證：建置 0 錯誤、單元測試 343 通過、1 略過。遊戲內未錄影。

### C6-1 配樂主題切換（SOUND.C change_music_maybe／set_new_music、AI.C damage_crit 與攻擊、COMBAT.C player_killed_a）
- 曲子播完、沒有新的要求時（`XMIMusic.RefreshMusic`）：
  - UW2：遊戲中除了戰鬥主題（2～4）和 0x18 以外，都改選該世界的漫遊曲。原本只有主題曲 1 和漫遊曲 8～15 會這樣，例如拔刀主題、勝利曲、死亡曲、10 號播完後都不會改選。
  - UW1：不會自己重播的主題（0、1、9～11）和漫遊曲改選隨機漫遊曲，其他重播；拔著刀時一律換成拔刀主題，包括戰鬥曲播完時。原本戰鬥曲會重播戰鬥曲。
  - 播完沒有新要求時重播：已照原版。原版的 load_new_music(0) 代表「目前這首」，同一首就從頭再播，所以遊戲外（主選單等）任何一首播完都會重播；之前文件寫「原版在主選單不會再播」是錯的。本專案的 LoadXMI 遇到同一首會直接略過，而循環又被關掉，所以原本的「重播」其實什麼都沒做，曲子播完就安靜了。新增 `XMIMusic.LoadNewMusic`，照 load_new_music 從頭再播，兩代播完的處理都改用它；UW2 也拿掉「遊戲外只重播主題曲 1」的特例。曲子檔讀不到時不再每格重試（原版會關掉音樂）。
- set_new_music：UW2 在勝利曲播放時忽略新的要求，UW1 不會。原本兩代都忽略。
- 戰鬥配樂的觸發：
  - 玩家受到攻擊時，HP 低於上限的 1/4 才換「危險」主題。原本 `play_hp << 6 / (max_hp + 1)` 的運算子優先順序錯了，HP 低於 16 才會換。
  - 打到生物：UW1 只要是玩家打的都算；UW2 要求不是打到自己。玩家受到任何來源的攻擊（不含 0）都算。原本只算 NPC 打的。
  - 致命一擊不換戰鬥配樂（crit_die 先返回），勝利曲由經驗值獎勵觸發。
  - 「最後一次戰鬥」的時間（lastcombattime）只在換戰鬥配樂時記錄。原本任何傷害都會記錄，連陷阱傷害都算，戰鬥配樂因此延長。
  - 生物開始攻擊玩家時，目前不是戰鬥主題才換：條件是小於 foe hurt（UW2 2、UW1 5）。原本寫成小於等於，會把 foe hurt 蓋成 combat。
- 沒有單元測試：這部分依賴 Godot 的音樂播放器。
- 其他呼叫點（睡覺、地圖、對話、復活、夢境）：已在「C6-1 其餘」逐一比對。
- 驗證：建置 0 錯誤、單元測試 343 通過、1 略過。遊戲內未錄影。

### C6-2 音效（SOUND.C play_effect／Maybe3DAudioSource、COMBAT.C、MISSILE.C、EFFECT.C、SOUNDS.DAT）
- 比對由唯讀子代理完成，逐項對照原版後修正。
- 左右聲道（`PositionalAudio.ComputePan`，只有 UW2 的數位音效用到）：
  - 原版是 `side = (sin·nx − cos·ny) >> 8`：cFstSinCos 先傳回 sin。本專案寫成 cos·dx − sin·dy，差了 90 度，正前方的聲音會偏到一邊。
  - UW1 的公式差一個負號（MIDI 的左右相反），但 TVFX 不用 pan，所以沒有影響。
  - 測試 `PositionalAudioTests` 改成原版的幾何：面向 0 時前後置中，±x 分在兩邊，正側的極值是 1 和 0x7F。
- 近戰打中生物的音效位置：改成 (格子 << 3)＋防守方的細座標。原本把格子座標當成細座標傳入，音源落在地圖原點附近，幾乎都被距離剔除。
- 玩家被打中的音量差值改用 ddone（含蓄力與側擊加成），原本用骰出來的原始傷害。
- UW2 玩家武器種類（GetPlayerWeapon）：拳頭 0、鈍器 1、利器 2。原本固定是 1，所以利器打中聽不到 0x1B，拳頭打牆也聽不到 8。
- UW2 打到牆時不再接著播揮空聲（hit_wall）。
- 發射物：
  - 弓弦聲（9）改成弓和十字弓才響（9、10）。原本誤用 8，結果變成投石索才響。UW1 不管有沒有射出都響；UW2 射出才響，再加上發射物上的 0xA（+0x14），投石索也有。
  - UW2 陷阱射出播 0xA（+0x14）。原本播成投擲物的 0x1C。
  - UW2 法術飛彈：酸液（6）和雪球（12）播 0xA，其他播 0x28（+0x14）。原本沒有音效。
  - NPC 射擊的額外 0xA（+0x14）只限 UW2。原本 UW1 會響兩次。
- 門關到底的聲音（toast_animobj）：
  - UW1 一律 0xC。
  - UW2 鐵柵門 0x26、其他 0xC，`SkipDoorSound`（quick_time）時不播。
  - 原本沒有這個聲音。
- SOUNDS.DAT 的 digi 位元組（UW2 第 5 位元組）：標成沒有數位音效的項目改播 MIDI，即使有 SPnn.VOC 也一樣（0x28／SP40.VOC）。`SoundEntry.Digi` 由 `SoundsDatLoaderTests` 驗證。
- 小項：
  - UW1 放不下物品的 0xF 音量 −10。
  - UW1 射程前方沒有空間時不出聲，只有 UW2 搖晃（0x2D）。
  - 被飛彈打中 pan 0（play_effect_here(3, 0, 0)）。
- 未做：
  - 播放中隨聲源重算 pan 與音量（update_digi_playback）。（優先權、停止音效、同時發聲數已在 C6-3 完成）
  - 單步移動（easy move）的腳步聲：已在「C3-5 其餘：單步移動」完成。
  - quick_time 靜音：SCD 關門原本就會設 `SkipDoorSound`；UW2 開門、關門的聲音（UseDoor 的 0xB／0x14）也跟著靜音，已在「C6-2 其餘」完成。對話結束時的 quick_time 只包住 8 步動畫補跑，本專案沒有那一步，不適用。
  - UW2 死亡音效的預設值 6：已在「C6-2 其餘」完成。
  - 格擋時用攻擊方盔甲種類的原版 bug、UW1 NPC 被飛彈打中時響兩次：在原版找不到明確出處，不照做。
  - 音量算出 0 時，UW2 原版不播：已在 C6-3 完成。
- 驗證：建置 0 錯誤，存檔測試 343 通過、1 略過，音效測試 84 通過。遊戲內未錄影。

### C6-3 音效引擎：槽位、數位聲道優先權、停止音效（SOUND.C fx_play／digi_fx_play／kill_effect／kill_all_effects）
- 新增 `Sfx.EffectChannels`（不依賴 Godot，可以測試），在遊戲端管理原版的聲道：
  - MIDI 音效占用一個槽位（UW1 4 個、UW2 3 個），持續 SOUNDS.DAT 的長度（1/256 秒），0xFFFF 一直占用。槽位全滿時新的音效直接丟掉。原本 TVFX 有 9 個聲部可用，戰鬥時會比原版疊得多。
  - UW2 的數位聲道：新音效的優先權（SOUNDS.DAT 的 priority＋音量，守護者 0x7D00）要大於或等於正在播的才能搶下聲道，否則改播 MIDI。原本新音效一律蓋掉舊的，例如腳步聲會切掉打擊聲和死亡聲。
  - SOUNDS.DAT 的 priority 改從 UW2 第 6、7 位元組讀取，算法是 low＋high×200（read_fx_data，`SoundEntry.Priority`）。
- 停止音效：
  - `UWsoundeffects.PlaySoundEffect*` 改成傳回控制代碼：MIDI 槽位 0～3、數位聲道 100、沒有播放時 −1。
  - 新增 `KillEffect`（放開該槽位的 TVFX 聲部，或停止數位聲道）、`KillAllEffects`。
  - TVFX 這邊：`SfxCommand` 加上 Slot 與 NoteOff，`TvfxVoice.Slot`／`NoteOff`，`TvfxVoiceAllocator.NoteOff`。
- 呼叫點：
  - 游泳水聲（make_noise）：記下控制代碼，每 0x1800 tick 先停掉再重播，出水就停。原本從來沒有停過，出水後還會響到結束，在水裡會疊播。
  - kill_all_effects：玩家死亡（兩代）、關閉音效（turn_fx(0)），以及 UW2 開自動地圖和進入對話時。
- UW2 音量算出 0 時不播（play_effect）。UW1 仍然占用槽位但不發聲，相當於原版的 velocity 0。UW2 patch 0 的效果不播。
- 測試：新增 `EffectChannelsTests`（槽位滿了丟掉、kill 立刻釋放、數位聲道優先權），以及 `TvfxVoiceAllocatorTests` 的依槽位放開聲部。
- 未做：
  - 播放中隨聲源重算數位音效的 pan 與音量（update_digi_playback）：已在「C6-2 其餘」完成。
  - 數位音效的 EMS 快取與循環次數：已在「C6-2 其餘」完成。
  - UW2 過場開始時停掉數位音效（punt_all_digi_fx）：已在「C4-1 其餘」完成。
- 驗證：建置 0 錯誤，音效測試 90 通過，存檔測試 343 通過、1 略過。遊戲內未錄影。

### UPK-11 其餘：睡覺時的物件清除與平靜（SKILLS.C player_sleep、OBJECTS.C Obj_GarbageCollect、CRITTIME.C）
- 新增 `ObjectRemover_OLD.GarbageCollect(range, count)`（Obj_GarbageCollect）：
  - 處理範圍：距離玩家超過 10 − range 格（曼哈頓距離）的格子，逐列、逐格依物件清單順序處理。
  - 刪除條件：照原有的剔除測試（`ObjectCullingTest`，即 Obj_Elem_Fate 加上 chkTenacious），通過才連同內容物一起刪除，最多刪 count 個。
  - 睡覺時以 range 1、20 個呼叫，位置在關門之後、時間流逝之前（兩代相同）。原本是 TODO。
- 生物平靜（update_all_critters_whilst_player_snoozes）：
  - 本專案的 `uwObject.ResetMobileObjects` 已經在睡覺沒有被打斷時呼叫，也用在離開樓層時。
  - 逐項對照 up_crit 都一致：清除暫時旗標、隨機面向（UW2 等待交談的除外）、回血到一半、依種族計數再調整態度、回到原本的格子要先通過 CanPlace、召喚出來的生物刪除。
  - up_mob 也一致：火球與防護球不動；其他會被剔除，或變成靜止物件落到地上。
  - 所以只拿掉過時的「Add calm function here」訊息。
- 沒有單元測試：要有載入的關卡與物件清單。
- 驗證：建置 0 錯誤、單元測試 343 通過、1 略過。遊戲內未錄影。

### C1-1 其餘：物件清單滿時的配置（OBJECTS.C Obj_Alloc、OBJPHYS.C mob_to_static、TRIGGER.C 建立物件機關）
- 配置（`ObjectFreeLists.GetAvailableObjectSlot`，Obj_Alloc）：清單空了先清一次遠處物件（`GarbageCollect`，range 3；移動物件 5 個、靜態物件 10 個），還是沒有才回傳 0。原本直接回傳 0。
- 拿到 0 的呼叫點不再寫進物件 0（物件 0 是「沒有物件」，被蓋掉之後所有接到 0 的串列都會壞）：
  - 物件落地（`ObjectHitsFloorTile_seg030_2BB7_DDF`，mob_to_static）：沒有空位時物件消失，回傳 null。原本把飛行中的物件複製進物件 0 再放到格子上。
  - 同一處：沒有留下物件時不引爆（原版 mts_doanim 收到 0）。原本 UW1 以太虛空裡的火球、閃電落地就會因為 null 當掉。
  - 建立物件機關（`a_create_object_trap.DoCreateObject`）：沒有空位時什麼都不建立。UW2 隨機生物那條路仍然把樣板換回冒險者；原版這時不換回，樣板會一直是那隻生物。這裡刻意不照做。
- 清除時如果剛好刪掉正要落地的物件本身，就把剛拿到的空位還回去，不再處理那個物件。原版沒有這個檢查，會處理已經釋放的物件；本專案會因此重複釋放、弄壞空閒清單。
- 未做：
  - 物品欄移回世界（`AddInventoryObjectToWorld`）沒有空位時寫進物件 0：已在下一節完成。
  - 靜態物件變成移動物件、建立物件的呼叫者：已在「C1-1 其餘：物件配置失敗的呼叫者」完成。
- 測試：`ObjectFreeListTests` 改成先建好關卡，新增兩個測試：沒有東西可清時回傳 0；清單空時清掉玩家 7 格外的物件，並拿到其中一個。
- 驗證：建置 0 錯誤、單元測試 344 通過、1 略過。遊戲內未錄影。

### C1-1 其餘：物品欄移回世界沒有空位時（`AddInventoryObjectToWorld`）
- 背景：原版身上的物品本來就在關卡物件清單裡，從背包拿出來不必配置，只有換層還原時（InvRestoreNexts、游標物件）才配置，而且原版不檢查。本專案的物品欄是另一個清單，每次移回世界都要配置，所以失敗時的處理是本專案自己的設計。
- 移動前先算整條需要幾個物件：物件本身、容器內容，`RemoveNext` 時連後面的也算（`CountInventoryObjects`）。
  - 空位不夠就照 Obj_Alloc 清遠處物件（range 3、每次 10 個），清到夠了或清不出東西為止。
  - 還是不夠就回傳 0，什麼都不改。原本會先把內容物一個個搬出去，到容器本身時拿到 0，寫進物件 0，物品欄也只搬了一半。
- 呼叫者拿到 0 時：
  - 從物品欄拿起（`uimanager.DoPickup`）：東西留在原處、手上沒有東西。
  - UW2 學院的念力魔杖（`academy.MoveTelekinesisWandToTile`）：魔杖留在身上。
  - 對話給 NPC（`give_ptr_npc` 背包那條路）：不給，回傳 0。
  - 換層時手上的物件（`Teleportation`）：放進一個空的物品欄格子，並計入負重（拿在手上的不算負重）。沒有空格就消失。
- 測試：新增 `InventoryToWorldTests`：
  - 空位不夠、又沒有東西可清時，回傳 0，袋子、內容物與物件 0 都不變。
  - 清單全空時，清掉遠處物件後，整袋（袋子和兩顆投石）都移到世界。
- 換層時手上的物件消失時容器內容留在物品欄陣列：已在「C1-1 其餘：物件配置失敗的呼叫者」完成。
- 驗證：建置 0 錯誤、單元測試 346 通過、1 略過。遊戲內未錄影。

### LOOK.C 查看與鑑定（LookAt、do_mods、do_of、SpecialLook、BookLook、BonesLook、KeyLook、talisman_desc；INTERACT.C player_3dlook／inv_look；BARTER.C；SPELLS.C obj_spells）
- 比對由唯讀子代理列出 23 項差異，逐條對照原版確認後修正。
- lore（鑑定程度）：
  - 3D 世界查看（`look.LookAtWorld`）：0x48 範圍內、不能撿起的物件給 1，其餘給 0，不擲骰、不寫朝向。原本一律擲 Lore，地上的東西不用撿起就看得到「magical」與附魔名稱，還會改寫世界物件的朝向。
  - 物品欄查看（`look.LookAtInventory`、`LoreCheck`）：UW1 對 10（原本兩代都對 8）。可鑑定的物品一律先擲骰再描述；原本鑰匙、書、寶石走自己的處理，不擲骰。
  - 交易：NPC 那側 1 + (skill_check(Lore, 20) > 0)，玩家那側 1 + skill_check(Lore, 15)，不讀寫朝向。原本 NPC 那側是 0，玩家那側用物品欄的檢定並寫朝向。
  - Name Enchantment（`spellcasting_class_7.NameEnchantment`）：一律以 lore 3 查看（門、告示等也印），之後才寫朝向 7，排除告示類、生物與平面圖。原本門和告示類什麼都不印，NPC 的朝向會被改成 7。
- 流程：
  - 原版先印描述行，再由 SpecialLook 追加一行。本專案原本各物件處理器是「取代」描述行，例如鑰匙在世界中只印名詞、書在物品欄只印內文、寶石寫死句子、UW1 釘死的門只印「門被釘死了」。改成照原版：
    - 可查看的物件印描述行（生物走 npc.LookAt）。
    - 不可查看的告示類（0x16x）走 RectLook：橋、墓碑、告示、材質。UW1 豎井只在近處（lore > 0）顯示。
    - 之後 SpecialLook：書（UW2 類別索引 10 以下）、鑰匙（block 5 0x64＋擁有者）、骨頭、關著且釘死的門（UW1 0x83；UW2 0x91 是空字串）。
  - 不可查看、又不是告示類的物件什麼都不印（原本仍印名稱，例如力場）。
  - 移除被取代的 `door.LookAt`、`doorkey.LookAt`、`smallblackrockgem.LookAt`；`Readable.LookAt` 改名 `ReadForUse`，只給使用（Use）時讀書用。
- 找陷阱：只有選了查看圖示（RightButtonThing 3）才找，classic 預設模式點擊不找；modern 操作維持每次都找。物品欄查看不找。先印描述再問（原本找到陷阱就不印描述）；提問改用 UW1 0xF4／UW2 0x103（原本 0x62／0x6F）。
- 物品欄查看先觸發查看機關再描述，機關清單改用物品欄（原本用地圖清單去索引物品欄物件的連結）。
- 描述行（`GetDescriptionString`）：
  - 冠詞：由名稱前的第一個字決定（magical、UW2 cursed、寶石的 warm／cool、品質字）。數量前不加，品質類型 13（腿甲、靴子等）不加。前面沒有字時用名稱自帶的冠詞（新增 `GameStrings.FixNameString`／`GetName`，照 fix_name_string），例如「the Key of Truth」、「acid」。原本一律用母音判斷，會出現「a the Key of Truth」「a worn leather boots」。UW1 的 cursed 照原版不決定冠詞。
  - 品質字：quality 1 也照一般分級（品質類別 3 用第 5 個字）；燈具 quality 1 不顯示的例外只有 UW2。
  - UW2：magical 也給類別索引 8 以下的魔法書；黑岩寶石的 warm／cool 改由 do_mods 加在描述行裡，擁有者 > 0 是 warm（原本 == 1）。
  - 擁有者：照 (owner & 0x1F) 上限 UW1 0x1B／UW2 0x1E。英文句尾補上句點。
- 附魔（do_mods／do_of）：
  - magical／cursed 的判斷不設 always_decode，沒有充能的法術物件有 4/10 機率看不出來；of 與充能才設。
  - of Poison：只有 UW2、只限 lore 3、藥水 0xE1～0xE7、非數量物件，在內容鏈（不遞迴）找傷害陷阱，優先於法術判斷。原本兩代都會、不限 lore、遞迴找，還可能出現「of of poison」。`MagicEnchantment.Describe` 的毒判斷移除。
  - 充能：在內容鏈的兄弟裡找法術物件（不遞迴），原本只看第一個內容物。字樣照原版：「with N full charge(s)」「with no full charges」；沒有名稱時是「UNNAMED」。
- 特殊物件：
  - 新增 BonesLook：「It looks to be that of 」（堆或多個時「They look to be those of 」，UW1 0x16／0x17、UW2 0x1A／0x1B）加上生物名（擁有者 + 0x40），0x3F 是 an adventurer；擁有者 0、0x28、0x3C～0x3E 不印。
  - 新增 UW1 talisman_desc：lore 3 時，fate 10 的九件聖物印 0x104＋0x105＋編號，不再印其他描述。
  - BookLook：UW1 地圖追加 0x97「Enscribed upon the scroll is your map.」；文字編號 > 0xFF 只印內文，否則先印「You read the <名稱>...」（原本用 link > 512 判斷）；有附魔的不讀；旗標 10 播過場。UW1 沒有名稱時印 UNNAMED。
  - 告示（class 6）：文字是數量物件的 link & 0x1FF，否則是擁有者，並保留冠詞（原本只處理數量物件，用 link − 0x200）。
- 中文：`ZhText.LookObject` 加上寶石的冷暖字；骨頭、聖物、書用各自的翻譯字串組句。
- LOOK.C 其餘（第二個 commit）：
  - 生物描述（`npc.LookAt`，CritterLook）改成單一套規則：
    - 種族名照 fix_name_string。
    - 心情只對 whoami < 0xF0 或 0xFF 顯示。
    - UW2 名稱空白且 goal 11 時用 0x115＋動作序列（原本用 whoami ≥ 240 判斷虛空生物）。
    - 名字小寫時取代種族，大寫時接「named」。
    - 句尾補上句點。
  - 使用書（`Readable.Use`，UseBook）：
    - 在世界中使用沒有動作（原版 how 為 0）。
    - 文字編號 < 0x100 時先印「You read the <名稱>...」再印內文；UW1 ≥ 0x100 是燉菜；有附魔的施法。
    - 原本 UW2 走查看的路，判斷前綴用 link > 512。移除 `ReadForUse`／`ReadObject`。
  - 符文袋右鍵：以 link、owner 為 0 的暫時符文石做 LookAt(…, 0)。
  - UW2 GetObjDesc（`look.GetObjDesc`）：品質字＋不帶冠詞的名稱，堆疊用複數。用在修補、附魔成功、附魔炸毀的英文訊息。研究怪物那句原本就和原版一致。
  - `MagicEnchantment.IsPotion` 改成原版範圍 0xE1～0xE7（只有 UW2），拿掉 UW1 分支。
  - 移除不再使用的 `GenericLookDescription`、`PrintLookDescription`。
- 測試：新增 `LookDescriptionTests`：fix_name_string 的冠詞與複數；用 UW1 的字串與 COMOBJ 驗證品質類型 13 不加冠詞、沒有前置字時用名稱自帶的冠詞、數量取代冠詞並用複數、品質字依首字母決定冠詞。
- 驗證：建置 0 錯誤、單元測試 356 通過、1 略過。遊戲內未錄影。

### C1-1 其餘：物件配置失敗的呼叫者（OBJPHYS.C static_to_mob、MAPADDR.C CreateObj 的呼叫者）
- 靜態物件變成移動物件（`MoveObjectToMobileObjectList_seg030_2BB7_B0C`，static_to_mob）：
  - 配置時的清除刪掉物件本身時，還回移動物件空位並回傳 null，兩個呼叫者跟著停下（`set_phys_data` 回傳 false、`PlacedObjectCollision` 回傳 null）。
  - 沒有空位時物件留在原處（原版回傳 0）。`PlacedObjectCollision` 原本照樣對這個靜態物件寫速度、方向與俯仰。靜態物件只有 8 個位元組，這會寫進下一個物件。現在直接跳到結尾。原版這裡會寫到空指標，不照做。
- 逐一檢查建立物件的 39 個呼叫點（`PrepareNewObject`、`spawnObjectInTile`、`SpawnObjectInHand`）。
  - 不會用到回傳值的（godry、翡翠謎題、販賣機、物件損壞的碎片、合成、食物剩菜）不改。
  - 會用到的 13 處補上失敗時的處理，沒有空位就不做：
    - 拆一疊（物品欄與地上各一處）。
    - 地震物件、召喚生物、虛空的月之門、UW2 死亡時的血跡。
    - 點燈拆出的那一個、油瓶。
    - Key of Truth 與 Cup of Wonder：沒拿到就不設旗標，Cup 的訊息也不印。
    - 競技場生物、暫時的對話者（Garamon、會說話的門、UW2 的 wisp）：呼叫者跟著檢查 null。
- 換層時手上的物件沒地方放而消失時，連同容器裡的東西一起從物品欄清掉。
- 沒有單元測試：這些都需要載入的關卡、介面或對話。
- 驗證：建置 0 錯誤、單元測試 357 通過、1 略過。遊戲內未錄影。

### DAMAGE.C 物件受損（damage_item、damage_object、remove_object、debris_type；INVDATA.C DamageInventory；COMBAT.C 暴擊磨損）
- 比對由唯讀子代理列出 18 項差異，逐條對照原版確認後修正。
- damage_object（`damage.DamageOtherObjectTypes`）：
  - id bit 13：UW1 任何物件都打不壞（原本兩代都用 UW2 規則，門照樣會壞）。
  - 依品質類別右移後沒有傷害就什麼都不做（原本照樣扣、照樣判壞，品質 0 的物件被 0 傷害打到也會壞並觸發機關；UW2 揮空也會被記冒犯）。
  - 壞掉時觸發使用機關，用傳入的來源（原本寫死 0），而且只限地圖上的物件。
- remove_object（`damage.ObjectDestruction`）：
  - 不在地圖上（物品欄）時什麼都不做、回傳「已離開」。原本物品欄的武器、盔甲會直接在身上變成碎片或斷掉的武器，訊息說「受損」。
  - 門：打開並移除鎖，回傳「沒有離開地圖」（原本回傳離開）。
  - 箱子、木桶：UW2 才看 id bit 13；之後照樣變成碎片（UW2 木屑）。
  - 武器變成斷掉的武器只有 UW2（UW1 走一般規則）；鑲寶石匕首是 0xA（原本寫 0x10）。
  - 袋子：先做 Obj_Elem_Fate(10)，裡面有不易消滅的東西就保持原樣；否則用 `DumpTheBag`（擁有者與偷竊判定）。原本直接灑出、擁有者 0。
  - 一般物件：火焰傷害才會燒掉碎片（UW1 0xD5、0xD6，UW2 0xD6），或 1/4 機率冒火（6d10）變成碎片。原本任何傷害都冒火、碎片一律直接刪掉。內容物釋放（原本只有 TODO）。
  - 碎片：UW1 0xD5／0xD6 隨機，UW2 debris_type。UW2 才把品質與血量重設成 40。之後用 obj_deal 重新安置（掉進水或熔岩就消失）。
- debris_type（`GetObjectTypeDebris`）：武器只限物品 0～15、只有匕首（3）是斷掉的匕首，家具 0xDC，其他 0xD6。原本整個主類別 0（盔甲、戒指、盾）都算武器。
- 精靈瓶（`djinnbottle.DestroyDjinnBottle`）：還沒抓到精靈時印 0x171 並受 0xFF 傷害，瓶子變成碎片；在法陣上吸收後才刪除；其他地方閃光後變成碎片。原本三種情況都直接刪掉，之後又對刪掉的物件設碎片。
- 裝備磨損（`DamageEquipment`）：
  - 壞掉時把碎片（UW1 隨機、UW2 debris_type 依傷害種類）用 put_at 範圍 6 放在玩家附近，原本放在格子正中央。
  - 英文訊息「Your <名稱> was/were damaged./destroyed.」，名稱以 s 結尾用 were，句尾有句點。
- 暴擊磨損（`combat.cs`）：頭部（0）維持頭盔，原本會變成副手；閃色 UW1 0xB8／UW2 0x23。
- `damage.DamagePlayer` 套用抗性的結果（原本算了丟掉，Flam 符文、睡覺時的毒照樣扣血）。
- Fire Doom 的範圍傷害來源是玩家（原本 0，不給經驗）；附魔失敗的爆炸打物件所在的格子（原本打玩家所在格）。
- UW2 地形升高壓碎物品的條件是 > 0x7F（原本 >=）。
- 未做：
  - 附魔失敗爆炸的 fireball_effect：已完成，爆炸生成後用 `animo.SpawnAnimoCopies` 撒出火焰（sp_enchant_faildestroymess）。
  - `HackTrapTrespass` 與 player_did_bad：已確認一致。原版以玩家所在格為中心，呼叫 player_grabbed(ThePlayer, owner)；本專案以玩家物件呼叫 `thief.FlagTheftToObjectOwner`，範圍（15×15、最多 20 個）與清除 owner 的規則相同，連原版會把玩家自己的 owner 欄位清掉這點也一樣。
  - `PlayerStatusUpdate` 與 FixPlayerEquips：已逐段對照。護甲與盾牌、防禦值、最亮的燈（含游標上的燈）、法術效果、亮度、致幻、虛空飛行都一致。修正一處：穿戴中的物品只有被動附魔才生效；「使用時施放」的法術（decode_obj_spell 的 flag，id bit 11）不算，原本 major 12 以上的這類法術會被當成防護或迷宮導航套用。UW1 的龍皮靴也照原版，只在物品沒有被動附魔時才設定，而且要穿在有效的格子。player_affected_by 兩代都一律回傳 0，所以 remove_spell 不會被呼叫。
  - 本專案多出的「陷阱與觸發器不受傷害」排除：已移除。COMOBJ.DAT 裡兩代的陷阱與觸發器（0x180～0x1BF）品質類別全都是 0，不是 3。原版的 damage_square（火球爆炸、附魔失敗）會打到格子裡的每個物件，陷阱被扣品質，扣到 0 就變成碎石堆（0xD6），連結的串列也一併釋放。現在照原版，所以火球打到機關所在的格子可能毀掉觸發器，與原版相同。
  - Flame Wind／Sheet Lightning：已逐一對照並修正（`spellcasting_class_6.cs`）。
    - 兩者的效果物件都照 build_new_obj，放在地板到 0x80 之間的隨機高度；原本固定在地板。
    - 傷害來源改成施法者（gronk_area 的 src），原本寫死 1，NPC 施法時算成玩家的。
    - UW1 Flame Wind（sp_meteor）：只在目標格放一個爆炸並撒出火焰（fireball_effect），十字形的五格都跑 damage_square。原本五格各放一個爆炸，沒有火焰。
    - UW2 Flame Wind：同一格連打或已打到 5 隻生物時回傳 0（不算一次）；原本算一次，法術會提早結束。加上 fireball_effect。
    - 次數、距離、半徑（UW2 area_spells 表、UW1 的 3d4、4、2）與傷害骰（10d6 型別 0xB、6d5 型別 3）原本就一致。
- 測試：新增 `DamageRulesTests`：UW1 id bit 13 連門也保護、右移後沒有傷害不會壞、物品欄的物件壞掉時保持原樣、debris_type 四種情況。
- 驗證：建置 0 錯誤、單元測試 364 通過、1 略過。遊戲內未錄影。

### PATHFIND.C 生物尋路（hyp_move、flood_path、beeline、路徑欄位、try_to_open_door、移動處理器；CRITTIME.C wander_that_monster、clear_paths）
- 比對由唯讀子代理列出 15 項差異，逐條對照原版確認後修正。
- 門：
  - NPC 用的鎖檢查改用照原版寫的 `a_lock.CheckLock`。原本的 `door.CharacterDoorLockAndKeyInteraction` 條件寫反，一律回傳「沒有鎖」：尋路永遠不會被門擋住，生物也撬不開鎖。舊函式移除。
  - hyp_move：UW1 對每扇關著的門都做門框判定，UW2 只對上鎖的（checkLock 0）。
  - 門框判定用朝向 & 3（原本 0～7），第 5、8 個方向判斷朝向 1（原本誤用 x 位置）。
  - 撞門撬鎖：checkLock(負的開鎖技能)，成功觸發解鎖機關並移除鎖。
- 危險值（落差）沿路累加（原本每步覆蓋）；落差分支遇到昂貴地形真的加 2 再判斷（原本只判斷沒加）。
- 離開禁止地形（例如走路生物在水上）：下一格也是禁止地形才失敗，下一格是昂貴地形才加 2（原本兩個條件都反了）。
- 斜坡方向索引是 path_turns[dx + 1][dy + 1]（原本 dy 正負號反了，南北向斜坡判斷顛倒）。
- beeline：兩個座標都到終點才停（原本任一個到就停，直線只檢查第一步）。
- 路徑的跳躍旗標讀 slopes[c / 8] >> (c % 8)（原本多乘 2）。
- 跳躍步（do_that_jump_kinda_thing）：朝向是「路徑格 + 下一步方向」減自己所在格（原本加在 fine 座標上、方向相反）；俯仰 0x16（原本 16）；設 didmove，結尾不會把速度改回走路速度。
- 失去控制（掉落、跳躍中）時，路徑當前格等於自己所在格就推進（原本比對寫死的 −1，永遠不推進）。
- UW1 飛行生物的高度：只看目的地，俯仰直接設成 0x12、0xE 或 0xF～0x11 隨機（原本用 UW2 的寫法）。飛行處理器遇到 0x100 時設 hitwall（原本從來沒設）。
- 換層時清掉 16 個路徑欄位（`npc.ClearPaths`，clear_paths）。原本從來不重設，離開樓層時佔著的欄位會永久流失，用完後生物不再尋路。
- 睡覺時生物找上門（wander_that_monster）：起點與目標高度用 z >> 3（原本用原始 z，放大 8 倍）；尋路前設定該生物自己的移動處理器與高度（set_critter_vars，新增 `npc.SetCritterVarsForPathing`）。
- UW1：開始走路時影格一律 (frame + 1) % 4；淹死時停在第 3 格。
- 沒查到：beeline 傳給 hyp_move 的 dist 在原版是未初始化的變數（本專案用 0）；UW1 地形分類、line_of_sight、crit_drunkwalk、IsaDoor 沒有逐行比對。
- 沒有單元測試：尋路需要載入的關卡與生物。
- 驗證：建置 0 錯誤、單元測試 364 通過、1 略過。遊戲內未錄影。

### CONVVARS.C、CREATURE.C、SKILLCHK.C（對話變數、新生物初始化、技能檢定）
- 比對由唯讀子代理完成，逐條對照原版確認後修正。
- SKILLCHK：skill_check 的公式、門檻與回傳值都一致，不用改。
- CREATURE（`ObjectCreator.InitialiseCritter`，init_this_critter／creature_obj_init）：
  - 俯仰設 0x10（水平）。原本寫到 `npc_attack`（就是俯仰）設成 10，新生物一開始會多跑一段物理。
  - UW1 的動作序列是站立 0x20（原本 0，在 UW1 是戰鬥架式）。
  - 清掉舊目標（`npc_level`）、B15_7、B19_1（原本沒清，重複使用的槽位會留下舊值）。
  - 地形位元只有 UW2 清。
  - UW2 不論放在哪個清單，新生物都會初始化（CreateObj）。UW1 的 CreateObj 不初始化，但召喚術自己會初始化，所以移動物件清單的照舊。
- 召喚的生物 Y 細座標誤用了 X（`spellcasting_class_8.cs`），改正。
- CONVVARS（`ConversationVM.ExportVariables`，update_converse_data）：
  - 腳本沒有匯入某個變數時保留前一個讀到的值（bab_var_out 不寫入），原本寫 0，例如沒匯入 play_hp 會讓血量歸零。
  - 回傳 NPC 是否被留在敵對（attitude 0）。
- 對話結束（`CONVERSE.C`）：變數立刻匯出，然後捲軸等 0x1F4 tick（約 2 秒）或按鍵；NPC 敵對、或最後一句是 NPC 說的（convo_say_flag，`say_op.LastLineWasPlayers`），就一直等到按鍵。原本一律先等 1 秒再無限期等按鍵，按鍵後才匯出。
- 其餘三項已完成：
  - play_hp／play_mana 匯出時照原版直接寫入、不夾上限（`playerdat.SetHpAndManaFromConversation`）。原本經過 setter，會夾在最大值。
  - npc_health／play_health 照原版用 16 位元算 `(int16)(hp << 8) / avghit`，血量 128 以上會溢位成負值；avghit（玩家是最大血量）為 0 時給 0x80。原本是 32 位元，玩家最大血量 0 時會除以零。
  - 打字回答（babl_ask）：已確認原版不動 convo_say_flag（不算任何一方的最後一句）；本專案的 babl_ask 也不經過 say_op，一致。
- 沒有單元測試：要有載入的對話與生物。
- 驗證：建置 0 錯誤、單元測試 364 通過、1 略過。遊戲內未錄影。

### C2-1 其餘：在地上使用物件施法、物品欄物件的機關（OBJUSE.C checkSpell、checkTrap／delete_trap；WORLDEV.C inanimate_spell）
- 在地上使用帶法術的物件（`use.UseItemCastSpell` → `MagicEnchantment.CastObjectSpell(fromTheObject)`）：照 checkSpell 的 how 0，以物件本身為施法來源、在物件所在格、以使用者（玩家）為目標施放（inanimate_spell）。原本一律以玩家為施法者、在玩家所在格。
  - 注意：本專案的 UW2 治療等法術作用在施法者上（照原版），所以地上物件施的這類法術不再作用在玩家身上。需要遊戲內確認。
- 物品欄物件的使用機關觸發後刪除（delete_trap）：從物件的內容鏈拿掉，連同它的內容一起從物品欄刪除。原本只刪地圖上物件的機關，物品欄的會一直觸發。
- UW1 對銀樹使用（動畫物件 0xA）：在世界中、手上沒東西時收回成種子放到游標上（using_punt、place_new，與撿起它相同）。移動中的門（0xF）本專案不會變成動畫物件，不適用。
- 驗證：建置 0 錯誤、單元測試 364 通過、1 略過。遊戲內未錄影。

### GAMESTRN.C 名稱與訊息（fix_name_string、get_name 與用到它們的訊息）
- 比對由唯讀子代理完成（同時比對 LIGHTING、TEXTMAPS，見下一節），逐條對照原版確認後修正。
- `GameStrings.FixNameString` 與原版一致；舊的 `GetObjectNounUW` 單數會保留 some_／the_ 冠詞，以下訊息改用 `GetName`：
  - 食物味道「That <名稱>…」（get_name 0, 0），原本會出現「That some popcorn」。
  - 「Use <名稱> on what?」（UseThing；原本少了 what，`useon.cs`、`orbrock.cs`）。
  - UW2 販賣機：get_name(1, 0) 再把首字母轉大寫，原本出現「A some leeches」。
  - 偷竊被看到：NPC 自己的名字，否則帶冠詞的種類（get_name(npc, 1, 0)），UW2 首字母大寫，原本不帶冠詞。
  - UW2 偵測生物：「You detect 」＋名稱自帶的冠詞＋方向＋句點，原本寫死「a」（a imp）、沒有句點。
  - 沒有彈藥：UW1 是程式裡寫死的「Sorry, you have no 」（UW1 的字串 0xCF 是「You push the lever.」，原本印出這句），UW2 用 0xCF；後面接複數名稱與句點。
  - 容器太滿補句點。
- 對話的 identify_inv（新增 `look.ConversationItemName`，conv_inv_name）：照 describe 參數決定冠詞，數量大於 1 加數字，接 do_mods／do_of 的字；不加品質字與擁有者。原本用查看的描述。
- UW2 研究怪物：NPC 用 CritterLook 去掉名字，非 NPC 用「a 」＋GetObjDesc，句尾句點；非 NPC 的生命力是 3 | quality（原本 quality）；特例判斷是 whoami 100（原本寫成 0x100，永遠不成立）。
- UW1 對話標籤：whoami 有值但沒有名字時不顯示（UW2 照舊用種類）。
- 驗證：建置 0 錯誤、單元測試 364 通過、1 略過。遊戲內未錄影。

### LIGHTING.C、TEXTMAPS.C（亮度等級的選擇與材質對照）
- `set_light` 只依等級讀 SHADES.DAT，等級由 FixPlayerEquips 等呼叫端決定；`random_light` 是吃蘑菇的效果之一，原版沒有光源閃爍。
- 夜視的灰階：最後等級剛好是 5 才變灰（set_light），不管是誰給的。原本只要有夜視法術就變灰，加上更亮的光仍是灰的。
- Roaming Sight（漫遊之眼）：UW1 亮度直接是 6，UW2 取 6 與該層環境光的較大者。原本沒有套用。
- 水晶球（hack trap 2，crystal_ball）：看的時候亮度 6，看完重算；UW2 Search 剛好 45 時不顯示。
- 蘑菇（set_drugged）：一次服用只在開始時挑一種效果並維持到結束，第一次一定是 3D 扭曲，之後三選一（扭曲、前 8 個調色盤之一、亂光照表），結束時還原。原本每次狀態更新都重抽調色盤 1～3。本專案只畫得出調色盤那一種，另外兩種沒有畫面。
- 已確認一致：UW1／UW2 材質對照的配置、地板的地形查表、環境光（DL.DAT）的判斷。
- 其餘兩項已完成：
  - Tybal 迷宮換地板（set_maze、swap_tmap）：改的是該層材質表的地板第 4 格（`texture_map[52]`，以及材質區塊位移 104 的字），開著迷宮導航時換成 0x0C（貼圖 222），關掉時若是 0x0C 就換回 0x0E（224）。每次狀態更新都這樣判斷（原版 parse_spells 每次都呼叫），不再靠「上一次的狀態」，所以讀到法術已結束、地板卻還是 222 的存檔也會換回來。查看地板的名稱跟著變；UW1 存檔時已載入關卡的材質區塊（18～26）改用記憶體裡的資料寫出（SaveLevel 會寫回 floor_IDs），原本照抄原檔。
  - 存檔裡的 light 位元組（UW1 0x64、UW2 0x65）：照原版存「身上最亮的點著的燈的亮度 << 4」加上它的位置（0～3 是燈光格 5～8，4 是游標）。原本把含環境光、法術、遙視的實際亮度寫進高半位元組，低半位元組沒寫。實際亮度改存在記憶體（`playerdat.lightlevel`），讀檔後由狀態更新重算。
- 驗證：建置 0 錯誤、單元測試 364 通過、1 略過。遊戲內未錄影。

### C2-3 其餘：物品欄的放置規則與搜尋順序（INVPANEL.C ItemFitsSlot、BAGS.C PutObjectInBag、INVDATA.C FindObj）
- 容量：放進開著的袋子時，從開著的那個袋子往外，每一層都要裝得下（`container.TestContainerCanHold`，新增 `EnclosingContainer`）。原本只看目標容器。
- 點著的燈放到肩膀與手（ValidLightSlots，5～8）以外的格子、背包或袋子裡會熄滅（`PutOutLightOutsideLightSlot`）。
- 武器手（8 − 慣用手）不收一疊近戰武器（`ValidObjectForSlot`）。
- 搜尋順序（新增 `objectsearch.FindObj`）：紙娃娃 0～10、背包 11～18，再依格子順序深入各容器。原本照物品欄陣列編號找，可能找到別的那一個。用在原版用 FindObj 的地方：燉菜的碗、彈藥（兩處）、地圖碎片找地圖、物品欄機關。對話的 find_inv 原版是沿玩家的連結串列找，不改。
- 驗證：建置 0 錯誤、單元測試 364 通過、1 略過。遊戲內未錄影。

### C2-5 其餘：撿取與丟下（INTERACT.C mous_in_3d／pick_3d／check_around／look_nothing／player_3dget、BAGS.C DoSpecialActions、MISSILE.C ReturnObject）
- 預設模式（看完再拖曳）對撿不起來的東西：生物（UW2 另加 wisp）交談，其他使用（player_3dget 的 def_mode）。原本一律說撿不起來。
- 月石：撿起或丟下「是月石或裝著月石」的東西都會更新月石的樓層（HasOrIsObj，新增 `pickup.HasOrIsMoonstone`）。原本只看物件本身。
- 點到地圖（不是物件）時，拿取模式印「Nothing to get.」、交談模式印「You cannot talk to that.」（look_nothing，UW1 0x98 起、UW2 0xA6 起）。
- 麻痺時 3D 視窗的點擊整個不處理（UW2 mous_in_3d），丟東西也不行；UW1 在別處擋住輸入，效果相同。
- UW2 點到牆、地板或空處時，以方形螺旋往外找半徑 9（原版像素）內的物件（check_around）。
- 戰鬥模式時點擊不丟東西、行動清單滿時改成放在前面：原本就一致。
- 驗證：建置 0 錯誤、單元測試 364 通過、1 略過。遊戲內未錄影。

### C1-1 其餘：聖物丟進熔岩（OBJPHYS.C mob_to_static、EFFECT.C fireball_effect）
- UW1 把聖物丟進第 8 層中央熔岩：剩下的聖物數之後的每一個火球都比前一個高 4～11、落在原格或旁邊一格（原本都在同一格同一高度）。
- fireball_effect（`animo.SpawnAnimoCopies`，也用在火球命中）：3～5 個火焰（原本 2～4），物品編號 +1 或 +2（原本 +0 或 +1），fine 座標偏移重抽到格內為止（原本夾住），高度 −8＋(rand & 0xF)。
- 驗證：建置 0 錯誤、單元測試 364 通過、1 略過。遊戲內未錄影。

### C6-2 其餘：死亡音效與 quick_time（AI.C crit_die、USEITEMS.C UseDoor）
- UW2 生物死亡音效：種類 0 不出聲，1～4 照表，其他一律 6（crit_die 的 default）。原本其他種類不出聲。
- UW2 開門、關門的聲音在 quick_time（SCD 關門時 `SkipDoorSound`）不播。原本只有門關到底的聲音會擋。
- 驗證：建置 0 錯誤、單元測試 364 通過、1 略過。遊戲內未錄影。

### C3-4 其餘：點武器手拔刀（INVPANEL.C DoInventoryMouse、INTERACT.C toggle_fightmode／pick_fightmode）
- 手上沒拿東西時點空的武器手（8 − 慣用手），拔刀或收刀（`uimanager.ToggleFightModeFromWeaponHand`）。拔刀只在游泳時拒絕（pick_fightmode），不像攻擊圖示還擋 UW2 的麻痺與睡夢虛空。原本沒有這個操作。
- 驗證：建置 0 錯誤。遊戲內未錄影。

### C3-3 其餘：UW1 地圖樓層箭頭（AUTOMAP.C ChangeAutoMapLevel）
- UW1 的樓層箭頭可以一路翻到第 0x63（99）層；地牢以外的層顯示空白的地圖與層數，不能寫筆記。原本最多到第 9 層。
- 驗證：建置 0 錯誤。遊戲內未錄影。

### C3-1 其餘：主選單的「繼續旅程」與開場（MAINMENU.C real_start／do_intro_scene）
- 沒有任何存檔（四個槽都沒有 DESC）時，主選單只有三個按鈕，不顯示「繼續旅程」。
- 遊戲啟動時，開頭畫面（cs009）之後若沒有存檔，先播開場（UW1 cs000，UW2 cs000、cs001）再到主選單（`ReturnToMainMenuOrIntroduction`）。
- 驗證：建置 0 錯誤。遊戲內未錄影。

### C6-1 其餘：其他配樂呼叫點（SKILLS.C、AUTOMAP.C、CONVERSE.C）
- 逐一對照：開地圖（UW1 主題 13；UW2 不是漫遊主題才換漫遊）、睡覺（UW1 13、UW2 漫遊）、UW1 對話開始（13）、夢境醒來（UW2 10）原本就一致。
- UW2 對話結束：一律 set_random_walking_music(−1)（隨機漫遊主題）。原本只在拔刀時換成拔刀主題，其他不換。
- UW1 銀樹復活（do_resurrect）：換成主題 4；生命保留復活時給的「最大值減 2～4」。原本傳送到樹邊後又把生命補滿、也不換音樂。
- 驗證：建置 0 錯誤、單元測試 364 通過、1 略過。遊戲內未錄影。

### C1 其餘：Change_AnimPtr（EFFECT.C）
- 動畫物件在移動與靜止清單之間換記錄時（mob_to_static、static_to_mob），動畫清單裡第一個指向舊記錄的項目改指向新記錄（`animo.ChangeAnimPtr`）。原本只有 TODO，換清單後動畫會失去它的物件。
- 驗證：建置 0 錯誤。遊戲內未錄影。

### C1-2 其餘：其他 put_at 呼叫點（BARTER.C、BABLHACK.C、SKILLS.C）
- 交易結束時交易格剩下的東西（end_barter）：用 put_at 放在玩家附近（範圍 5），原本隨機放在玩家那一格、不檢查空間。
- 從 NPC 拿東西但玩家的交易格滿了（npc_barter_give）：放在 NPC 附近（範圍 5），放不下回傳 3，原本直接放在 NPC 那一格、一律回傳 2。
- 對話的 place_object x < 0：put_at 玩家那一格的角落（不帶 fine 位置）、先試原位、範圍 6、不會被剔除。原本放在玩家的確切位置。
- 死亡時游標上的東西（punt_mouse_obj）：放到玩家附近（範圍 6），原本沒有處理。
- UW2 死亡的血跡：只在開放格，put_at 範圍 0，fine x／y 照原版對調。原本不檢查格子、也沒有接進格子的物件串列。
- 地震法術（SPELLS2.C tremor_area）：速度改成 (rand & 3) + 2（2～5，原本 2～4）。原版生成靜態物件再用 put_at 範圍 0 讓 obj_deal 把它變成掉落中；本專案直接生成移動物件，讓物理正常運作，保留這個做法。
- 驗證：建置 0 錯誤、單元測試 364 通過、1 略過。遊戲內未錄影。

### C5-2 其餘：DOS 讀本專案存檔的相容（MAP.C Map_Save／Anim_Save、EFFECT.C Anim_Save、ARC.C put_arc）
- 比對由唯讀子代理完成（exhumed 的這些檔案都在 matched.txt，可以當原版行為）。
- UW1 動畫清單（層 + 8 的區塊，64 筆）：寫出時把還在跑的排到前面、其餘清零（和 UW2 共用 `CompactOverlays`）。DOS 讀到第一個物件編號 0 就停；原本中間的空洞會讓後面的動畫遺失，跑完但還留著編號的會被 DOS 當成進行中，帶 0x20 旗標的動畫還會把物件移除。
- UW2 計時器清單（0x7D88，64 個字）：第一個 0 之後全部清零（Anim_Save）。本專案平常就會清，這是保險。
- UW2 LEV.ARK 檔頭：
  - 每個區塊保留來源的旗標 bit 0（壓縮）與 bit 2（保留空間），清掉 bit 1（因為寫的是未壓縮資料）；不存在的區塊也一樣。原本全部寫 0。
  - 存在的區塊配置空間等於長度。原本沿用來源的配置空間，但區塊是緊密排列的：配置比長度大時，DOS 下次寫這個區塊會走重建、多跳過幾個位元組吃掉下一個區塊，後面的位移全部錯開（實例：本專案寫的 SAVE2 第 152 個區塊長度 0x86、配置 0x88）。
- 未做：
  - ObjCrunch：已在「C5-2 其餘：ObjCrunch」完成。
  - UW2 空白存檔名稱：已在「C5-2」完成（UW2 寫一個空白，UW1 寫 0 位元組）。
- 測試：新增 `LevArkWriter_Uw2_EveryBlockKeepsItsFlagsAndReservesItsOwnLength`、`OverlayCompactionTests`；不存在的筆記區塊旗標的期望由 0 改成 5。
- 驗證：建置 0 錯誤、單元測試 366 通過、1 略過。沒有在 DOS 上實際讀過本專案的存檔。

### C3-6 其餘：輸入一行的編輯（SCROLLIO.C wdialog）
- Ctrl+A／E 到開頭／結尾、Ctrl+B／F 左右移、Ctrl+D 刪除游標處、Ctrl+K 刪到結尾（`main.WdialogEditKey`）；Home／End、方向鍵、Backspace、Delete 由輸入框原本就處理。
- 有預設文字時（數量提問的「1」），第一個輸入的字取代它；先移動游標就保留（wdialog 的負位置）。
- 數量提問只接受數字（anychar 0）；最多 50 個字。
- 驗證：建置 0 錯誤。遊戲內未錄影。

### C3-5 其餘：單步移動（PHYSICS.C simple_fizix、PLAYMOVE.C player_simple_move、PLAYER.C）
- `CanPlace` 輸出 can_place 的落腳高度與地形（`motion.NvokHgt`／`NvokTerr`：1 << 地板地形、空中 0x10、站在物件上 1）。
- 新增 `motion.SimpleFizix`：
  - 轉向 ±1 是 45 度。
  - 0 是半格前進，不能走進另一種地形或走下邊緣；2 是同樣的一步但不檢查；−2 是後退四分之一格。
  - 用 can_place 檢查後直接移動：落在可站的高度，或開始下落。
  - 只在不是下落中、速度（`momentum_14`）低於 MaxPlayerAccel（`MotionWeightRelated_dseg_67d6_C8`）時做。
- classic 的大寫 A／D／S／W／X（Shift）照 PLAYER.C 對應 −1／1／0／2／−2，每 0x18 tick 最多一步。原本只有 A／D 轉向，而且下落中也會轉。
- 羅盤上方的三個箭頭（原版畫面 0x6B～0x7B、0x82～0x92、0x9B～0xAA）：左轉、前進一步、右轉。這三個區域是否都在本專案的羅盤控制項範圍內，要遊戲內確認。
- 單步後跑一輪 0x40 的世界（`main.PlayerSimpleMove`，player_simple_move）：
  - 轉向也算，只要 simple_fizix 成功就跑。
  - 玩家速度歸零、動畫物件前進一格、遊戲時鐘加 0x40、移動物件的格數 frame_inc 加 4（加速時減半、單數留到下次）。
  - 接著跑 move_physics 的 easy 版：不跑玩家自己的物理，地面效果照跑，噪音加 4（set_sound），腳步聲一定響一聲並換腳，下一步計時設在 100 tick 後（make_noise）。原本單步完全沒有聲音，也不推進世界。
- 驗證：建置 0 錯誤、單元測試 366 通過、1 略過。遊戲內未錄影。

### C3-5 其餘：遙視與指標限制（PLAYMOVE.C player_mous_move／parse_playin、PLAYER.C move_cam）
- 遙視（Wizard Eye／Roaming Sight）時，按住左鍵在 3D 視窗移動眼睛（move_cam(0)，`main.RoamingSightMouseMove`）：指標在視窗的左、中、右三分之一，眼睛轉 −0x400、0、+0x400；由下往上的三分之一後退、不動、前進。原本滑鼠完全不移動眼睛。
- 鍵盤：UW2 移動眼睛（原本兩代都是）；UW1 的鍵盤照樣移動身體。
- classic 按住左鍵移動時，指標被拉回 3D 視窗內（mouse_constrain），放開才解除。
- 驗證：建置 0 錯誤。遊戲內未錄影。

### C3-5 其餘：鍵盤當滑鼠（MOUSE.C keyboard_mouse／mouse_btns、PLAYER.C、UWEDIT.C graceful_exit）
- classic 才有（modern 保留自己的鍵位）。新增 `main.KeyboardMouse`：
  - Tab／Shift+Tab：指標在 3D 視窗 (100, 130)、物品欄 (270, 120)、右下角 (305, 7) 三處循環（原版座標，y 由下往上）。
  - 數字鍵盤 1～9（5 除外）：指標往對應的邊或角移動，按住期間持續移動，連按加速到 40（`WarpMouseStep`）。
  - INS、DEL：在指標處按一下左鍵、右鍵。
  - Alt+H：對調左右鍵（mouse_hand）。按鍵事件會對調；`Input.IsMouseButtonPressed` 讀的是實體按鍵，按住移動這類地方不會跟著對調。
  - Alt+X：直接離開遊戲（graceful_exit）。
- 驗證：建置 0 錯誤。遊戲內未錄影。

### C3-1 其餘：選項面板的鍵盤（WRAPPER.C busywaiting_new_options）
- 選項面板開著時（`uimanager.OptionsPanelKey`）：上、左（數字鍵盤 8、4）把選取往前移，空白、右、下（2、6）往後移，Enter 按下選取的按鈕，Esc 回到遊戲。按鈕的點擊處理抽成 `PressOptionButton`，滑鼠與按鍵共用。
- 選取的按鈕畫得亮一點；原版用亮起的那張按鈕圖，這裡沒有用。
- Alt+X：遊戲中直接離開（見「鍵盤當滑鼠」那一節）。原版在離開確認頁按 Alt+X 是「是」，結果相同。
- 主選單（MAINMENU.C parse_start_input，`MainMenuKey`）：右、下、Ctrl+F／N 往後，上、左、Ctrl+B／P 往前，Home／PgUp 第一個、End／PgDn 最後一個，Enter 按下；一開始選在「建立角色」（沒有存檔）或「繼續旅程」，用亮起的按鈕圖表示。
- UW2 頂層頁（ICONS.C gameopts_buttongroup）：第 5 個按鈕是離開、第 6 個是回到遊戲（UW1 相反）。本專案原本兩代都用 UW1 的順序，UW2 的按鈕圖又是從背景第 5、6 格裁的，畫面上兩個位置對調。按鈕圖與點擊都改照 UW2 的順序；快捷鍵用的功能編號不變。
- 驗證：建置 0 錯誤。遊戲內未錄影。

- 其後依序：A、B 類已全部處理，C 類 TODO-1～6 已比對完；還沒做的整理在「剩餘項目總表」。接下來是所有修正的遊戲內錄影驗證。
