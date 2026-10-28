# UW2.EXE 對照資料（test vectors）

這裡的 CSV 取自 [abedegno/underworld-exhumed](https://github.com/abedegno/underworld-exhumed) 的 `uw2/vectors/`（commit `e5deadd6`，MIT 授權，Copyright (c) 2026 abedegno）。內容是用 UW2.EXE 自己的程式碼，在合成輸入上算出的數字，不含遊戲的程式或資料。格式與每份表的說明見原 repo 的 `uw2/vectors/README.md`。

- `seed`／`seed_out`：Turbo C `rand()` 呼叫前後的 32 位元狀態（`seed = seed * 0x015A4E35 + 1`，回傳第 16～30 位元）。
- 使用的測試：`tests/Underworld.Save.Tests/Uw2VectorTests.cs`。
