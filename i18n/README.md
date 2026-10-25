# 翻譯與中文字型

遊戲顯示用的譯文（見 `src/utility/localization.cs`）與中文字形來源（見 `src/loaders/cjkglyphs.cs`）。
設定 `i18npath` 留空時就用這個資料夾；`.gdignore` 讓 Godot 編輯器不匯入這裡的檔案，程式直接讀檔。

| 檔案 | 來源 | 授權 |
| --- | --- | --- |
| `uw1/zh-TW/master.tsv` | [pmanyeh/underworld-exhumed](https://github.com/pmanyeh/underworld-exhumed) `feature/zh-tw-native` 分支 `uw1/i18n/zh-TW/master.tsv`，commit f5b088b | MIT，見 `uw1/zh-TW/LICENSE-underworld-exhumed.txt` |
| `fonts/jf-openhuninn-2.1.ttf` | [justfont/open-huninn-font](https://github.com/justfont/open-huninn-font) v2.1 release，SHA-256 `9d5bf4932d31fe94c18cd8cfddc98bc1b14ce10f4e354c682179db290a99c825` | SIL OFL 1.1，見 `fonts/jf-openhuninn-OFL.txt` |

`master.tsv` 的欄位：block、index、category、source_hash（英文原文 CP437 位元組的 SHA-256）、target_zh_TW、status、context、notes、flags。
跳脫字元與 exhumed 相同（`\\`、`\t`、`\r`、`\n`、`\0`）。更新譯文時直接換掉這個檔案；原文雜湊對不上的列會顯示英文。
