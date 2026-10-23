"""
RTS Icons Generator (Solid Tactical Edition)
Generates high-detail, solid-filled 2.5D graphical icons for Godot RTS HUD, buildings, and battlefield tech.
"""
import sys
import os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from tools.generate_solid_rts_icons import generate_all

if __name__ == "__main__":
    generate_all()
