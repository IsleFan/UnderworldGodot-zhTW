# 把一批譯文併入譯文表。
#
# 用法：python3 -I add_translations.py 字串.tsv 譯文.tsv master.tsv [備註]
#   字串.tsv  dump_strings.py 從自己的遊戲資料匯出的（block、index、source_hash、english）
#   譯文.tsv  每行「block<TAB>index<TAB>中文」，跳脫與 master.tsv 相同（\n、\t、\\）；# 開頭的行略過
#   已有的同一列會被取代；原文雜湊一律取自字串.tsv，對不上任何字串的行會報錯。
import collections
import re
import sys

from reuse_uw1 import HEADER, category, read

dump_path, batch_path, master_path = sys.argv[1:4]
note = sys.argv[4] if len(sys.argv) > 4 else ''

hashes = {(r[0], r[1]): r for r in read(dump_path)}
try:
    rows = {(r[0], r[1]): r for r in read(master_path)}
except FileNotFoundError:
    rows = {}

count = 0
with open(batch_path, encoding='utf-8') as f:
    for n, line in enumerate(f, 1):
        line = line.rstrip('\n')
        if not line.strip() or line.startswith('#'):
            continue
        parts = line.split('\t')
        if len(parts) != 3 or not parts[2]:
            sys.exit(f'{batch_path}:{n}: 需要 block、index、中文三欄')
        key = (parts[0], parts[1])
        if key not in hashes:
            sys.exit(f'{batch_path}:{n}: 遊戲資料裡沒有 {key[0]}/{key[1]}')
        english = hashes[key][3]
        text = parts[2]
        # 原文結尾的換行要照留，訊息的分段靠它。（拼接用的空格不必：中文不靠空格斷詞，UW1 的譯文也不留。）
        if english.endswith('\\n') and not text.endswith('\\n'):
            sys.exit(f'{batch_path}:{n}: 原文以換行結尾，譯文也要：{english!r}')
        # 對話 VM 在執行時替換的標記（@GS8 玩家名字、@SS1、@GI3C2…）與顏色碼（\\1、\\0）必須原樣保留。
        tokens = lambda t: collections.Counter(re.findall(r'@[A-Z]{2}\d+(?:C\d+)?|\\\\[0-9a-z]', t))
        if tokens(english) != tokens(text):
            sys.exit(f'{batch_path}:{n}: 標記不符，原文 {sorted(tokens(english).elements())}，譯文 {sorted(tokens(text).elements())}：{english!r}')
        rows[key] = [key[0], key[1], category(int(key[0])), hashes[key][2], text, 'draft', '', note, '']
        count += 1

with open(master_path, 'w', encoding='utf-8', newline='\n') as f:
    f.write(HEADER + '\n')
    for key in sorted(rows, key=lambda k: (int(k[0]), int(k[1]))):
        f.write('\t'.join(rows[key]) + '\n')
print(f'併入 {count} 條；譯文表共 {len(rows)} 條：{master_path}')
