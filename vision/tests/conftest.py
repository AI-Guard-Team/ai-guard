import sys
from pathlib import Path

# Testler vision/ klasöründen "pytest" ile çalışıyor; main.py'nin import edilebilmesi için.
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
