import json
from pathlib import Path

# 1. Generate .graphifyignore for TAuto
ignore = """
bin/
obj/
.git/
*.dll
*.exe
*.pdb
*.webp
*.png
*.jpg
""".strip()
Path('.graphifyignore').write_text(ignore)

# 2. Run detection equivalent
# Focusing on the actual logic folders
code_files = [str(p) for p in Path('.').rglob('*.cs') if 'bin/' not in str(p.as_posix()) and 'obj/' not in str(p.as_posix())]
doc_files = [str(p) for p in Path('.').rglob('*.md') if '.git/' not in str(p.as_posix())]

detect = {
    'files': {
        'code': code_files,
        'document': doc_files
    },
    'hashes': {}
}
Path('.graphify_detect.json').write_text(json.dumps(detect, indent=2))
print(f'Detected {len(code_files)} code files and {len(doc_files)} doc files in TAuto.')
