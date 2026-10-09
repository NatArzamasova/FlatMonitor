#!/usr/bin/env python3
import os
import sys
from pathlib import Path
from dotenv import load_dotenv

print(f"Current directory: {os.getcwd()}")
print(f".env file exists: {Path('.env').exists()}")

# Try loading from explicit path
env_path = Path.cwd() / '.env'
print(f"Loading from: {env_path}")

result = load_dotenv(env_path, verbose=True)
print(f"Load result: {result}")

api_key = os.getenv("ANTHROPIC_API_KEY")
print(f"API Key found: {bool(api_key)}")

# Try reading raw
with open('.env', 'r', encoding='utf-8') as f:
    content = f.read()
    print(f"File content: {repr(content)}")
