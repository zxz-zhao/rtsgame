import os
import time
import hmac
import hashlib
import base64
import json
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.backends import default_backend

def get_sha256(text: str) -> str:
    """Generate SHA256 hex digest."""
    return hashlib.sha256(text.encode("utf-8")).hexdigest()

def sign_data(data_str: str, secret_key: str) -> str:
    """Generate HMAC-SHA256 signature."""
    mac = hmac.new(secret_key.encode("utf-8"), data_str.encode("utf-8"), hashlib.sha256)
    return mac.hexdigest()

def verify_signature(data_str: str, signature: str, secret_key: str) -> bool:
    """Constant-time signature verification."""
    expected = sign_data(data_str, secret_key)
    return hmac.compare_digest(expected, signature)

def encrypt_aes_gcm(plaintext: str, key_str: str) -> str:
    """Encrypt plaintext using AES-256-GCM. Returns base64 string containing iv + tag + ciphertext."""
    key = hashlib.sha256(key_str.encode("utf-8")).digest()
    iv = os.urandom(12)
    encryptor = Cipher(
        algorithms.AES(key),
        modes.GCM(iv),
        backend=default_backend()
    ).encryptor()
    
    ciphertext = encryptor.update(plaintext.encode("utf-8")) + encryptor.finalize()
    tag = encryptor.tag
    payload = iv + tag + ciphertext
    return base64.b64encode(payload).decode("utf-8")

def decrypt_aes_gcm(b64_payload: str, key_str: str) -> str:
    """Decrypt AES-256-GCM base64 payload."""
    try:
        payload = base64.b64decode(b64_payload.encode("utf-8"))
        if len(payload) < 28:
            return ""
        iv = payload[:12]
        tag = payload[12:28]
        ciphertext = payload[28:]
        key = hashlib.sha256(key_str.encode("utf-8")).digest()
        
        decryptor = Cipher(
            algorithms.AES(key),
            modes.GCM(iv, tag),
            backend=default_backend()
        ).decryptor()
        
        decrypted = decryptor.update(ciphertext) + decryptor.finalize()
        return decrypted.decode("utf-8")
    except Exception:
        return ""
