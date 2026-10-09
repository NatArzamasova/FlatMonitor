#!/usr/bin/env python3
import os
from dotenv import load_dotenv

load_dotenv()

api_key = os.getenv("ANTHROPIC_API_KEY")
if api_key:
    print(f"API key loaded: {api_key[:10]}...")
    if len(api_key) < 20:
        print("⚠️  API key seems too short!")
else:
    print("❌ No API key found in .env")
