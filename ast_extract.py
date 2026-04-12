import sys, json
from graphify.extract import collect_files, extract
from pathlib import Path

code_files = []
detect_path = Path('.graphify_detect.json')
if not detect_path.exists():
    print('Detection file missing')
    sys.exit(1)

detect = json.loads(detect_path.read_text())
for f in detect.get('files', {}).get('code', []):
    item = Path(f)
    if item.exists():
        code_files.extend(collect_files(item) if item.is_dir() else [item])

if code_files:
    result = extract(code_files)
    Path('.graphify_ast.json').write_text(json.dumps(result, indent=2))
    print(f"AST: {len(result['nodes'])} nodes, {len(result['edges'])} edges")
else:
    Path('.graphify_ast.json').write_text(json.dumps({'nodes':[],'edges':[],'input_tokens':0,'output_tokens':0}))
    print('No code files - skipping AST extraction')
