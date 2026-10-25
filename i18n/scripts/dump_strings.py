# 用法：python3 -I dump_strings.py STRINGS.PAK 輸出.tsv
# 照 GameStrings.LoadStringsPak 解碼，輸出 block、index、原文位元組 SHA-256、原文（\n 等跳脫）
import sys, struct, hashlib
d = open(sys.argv[1], 'rb').read()
n = struct.unpack_from('<H', d, 0)[0]; p = 2
nodes = [d[p + 4*i: p + 4*i + 4] for i in range(n)]; p += 4*n
nb = struct.unpack_from('<H', d, p)[0]; p += 2
blocks = []
for i in range(nb):
    no, addr = struct.unpack_from('<HI', d, p); p += 6
    blocks.append((no, addr, struct.unpack_from('<H', d, addr)[0]))
out = open(sys.argv[2], 'w', encoding='utf-8')
out.write('block\tindex\tsource_hash\tenglish\n')
for no, addr, cnt in blocks:
    q = addr + 2 + cnt * 2
    for j in range(cnt):
        s = bytearray(); bit = 0; raw = 0  # 每條字串從新的位元組開始
        while True:
            node = n - 1
            while nodes[node][2] != 255 and nodes[node][3] != 255:
                if bit == 0: bit = 8; raw = d[q]; q += 1
                node = nodes[node][3] if raw & 0x80 else nodes[node][2]
                raw = (raw << 1) & 0xFF; bit -= 1
            if nodes[node][0] == ord('|'): break
            s.append(nodes[node][0])
        if s:
            esc = bytes(s).decode('cp437').replace('\\', '\\\\').replace('\t', '\\t').replace('\n', '\\n').replace('\r', '\\r')
            out.write(f'{no}\t{j}\t{hashlib.sha256(bytes(s)).hexdigest()}\t{esc}\n')
