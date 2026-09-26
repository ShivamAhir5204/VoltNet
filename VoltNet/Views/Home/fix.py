import re
import sys

try:
    with open('Index.cshtml', 'r', encoding='utf-8') as f:
        content = f.read()

    # Smooth transition using CSS
    content = re.sub(
        r'style=\"width: 100%; height: 100%; object-fit: contain;\"',
        'style=\"width: 100%; height: 100%; object-fit: contain; transition: opacity 0.2s ease-in-out;\"',
        content
    )

    with open('Index.cshtml', 'w', encoding='utf-8') as f:
        f.write(content)
        
    print('Done!')
except Exception as e:
    print('Error:', e)
