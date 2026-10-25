# 用 UW1 的譯文補上 UW2 譯文表裡原文完全相同、還沒翻譯的字串。
#
# 用法：python3 -I reuse_uw1.py UW1字串.tsv UW2字串.tsv i18n/uw1/zh-TW/master.tsv i18n/uw2/zh-TW/master.tsv [來源標籤]
#   來源標籤預設 UW1；把 UW2 自己當來源（帶入 UW2 裡原文相同、已翻過的句子）時傳 UW2。
#   UW1字串.tsv、UW2字串.tsv 是 dump_strings.py 從自己的遊戲資料匯出的（含英文，不放進 repo）。
#   UW2 的 master.tsv 不存在時會建立；已有的列一律保留，只補沒有的。
import sys

HEADER = 'block\tindex\tcategory\tsource_hash\ttarget_zh_TW\tstatus\tcontext\tnotes\tflags'
CATEGORIES = {1: 'game', 2: 'chargen', 3: 'books', 4: 'object_names', 5: 'object_look', 6: 'spells',
              7: 'conversation_common', 8: 'writings', 9: 'trap_text', 10: 'textures'}


def category(block):
    if block >= 3584:
        return 'conversation'
    if block >= 3072:
        return 'cutscene'
    return CATEGORIES.get(block, 'game')


def read(path):
    with open(path, encoding='utf-8') as f:
        return [line.rstrip('\n').split('\t') for line in f.readlines()[1:] if line.strip()]


def main():
    uw1_dump, uw2_dump, uw1_master, uw2_master = sys.argv[1:5]
    label = sys.argv[5] if len(sys.argv) > 5 else 'UW1'

    translated = {(r[0], r[1]): r[4] for r in read(uw1_master) if len(r) > 4 and r[4]}
    english_to_zh = {}
    for block, index, _, english in read(uw1_dump):
        if (block, index) in translated:
            english_to_zh.setdefault(english, (translated[(block, index)], f'{block}/{index}'))

    try:
        rows = {(r[0], r[1]): r for r in read(uw2_master)}
    except FileNotFoundError:
        rows = {}

    added = 0
    for block, index, source_hash, english in read(uw2_dump):
        if (block, index) in rows or english not in english_to_zh:
            continue
        text, origin = english_to_zh[english]
        rows[(block, index)] = [block, index, category(int(block)), source_hash, text, 'draft', '', f'沿用 {label} {origin}', '']
        added += 1

    with open(uw2_master, 'w', encoding='utf-8', newline='\n') as f:
        f.write(HEADER + '\n')
        for key in sorted(rows, key=lambda k: (int(k[0]), int(k[1]))):
            f.write('\t'.join(rows[key]) + '\n')
    print(f'沿用 {label} 譯文 {added} 條；UW2 譯文表共 {len(rows)} 條：{uw2_master}')


if __name__ == '__main__':
    main()
