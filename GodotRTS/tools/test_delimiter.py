# -*- coding: utf-8 -*-
import codecs
import json

raw_from_db = r'\\n\\n'
print("raw_from_db repr:", repr(raw_from_db))
decoded = codecs.decode(raw_from_db, 'unicode_escape')
print("decoded repr:", repr(decoded))
print("Is actual newline?", decoded == '\n\n')

proper = r'\n\n'
print("\nproper repr:", repr(proper))
decoded_proper = codecs.decode(proper, 'unicode_escape')
print("decoded_proper repr:", repr(decoded_proper))
print("Is actual newline?", decoded_proper == '\n\n')
