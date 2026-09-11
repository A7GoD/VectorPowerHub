import glob

for f in sorted(glob.glob('src/*.cs')):
    with open(f, 'r', encoding='utf-8', errors='ignore') as fp:
        lines = len(fp.readlines())
    if lines > 150:
        print(f"{f}: {lines} lines")
