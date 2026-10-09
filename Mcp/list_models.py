#!/usr/bin/env python3
"""
List available models from Nexos API
"""

import json
import os
from pathlib import Path
import httpx

# Load API key from .env file
env_file = Path(__file__).parent / ".env"
if env_file.exists():
    with open(env_file, 'r') as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith('#') and '=' in line:
                key, value = line.split('=', 1)
                os.environ[key.strip()] = value.strip()

NEXOS_API_KEY = os.getenv("ANTHROPIC_API_KEY")
NEXOS_BASE_URL = "https://api.nexos.ai/v1"

if not NEXOS_API_KEY:
    print("ERROR: ANTHROPIC_API_KEY not set")
    exit(1)

headers = {
    "Authorization": f"Bearer {NEXOS_API_KEY}",
    "content-type": "application/json"
}

print("Fetching available models from Nexos API...")
print(f"Endpoint: {NEXOS_BASE_URL}/models\n")

try:
    with httpx.Client() as client:
        response = client.get(
            f"{NEXOS_BASE_URL}/models",
            headers=headers
        )
        response.raise_for_status()
        data = response.json()

    print("Available Models:")
    print(json.dumps(data, indent=2))

except httpx.HTTPError as e:
    print(f"HTTP Error: {e}")
    if hasattr(e, 'response') and e.response is not None:
        print(f"Response: {e.response.text}")
