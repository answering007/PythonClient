# -*- coding: utf-8 -*-
"""
Created on Sat Nov 21 16:18:40 2020

@author: Pike
"""

query_text = globals()['query'] if 'query' in globals() else str('H4sIAAAAAAAEAF2STWvcQAyG/4vPy6KvkUa5hbDQHJoeAr2UHpziZg2z3eBsF9LS/16F4plxj7b1WNKr58vv4eE83OBueBhP03AzfCrzdR6H3fB5LD/jBfzZ/SuhWnJ77b/v1RnFMENK7pLIV4Ircf86Pk2ldBjuwbMiUc6qGeNhpaRSj+eX47xhOCu5sCKiZwddmVSZu+O4lPPlMvWYQkDmSMKCALxiWrGP2z5Ra8lFPeaLtdZ6axGcprJFYnNHQPGoZ2RamVyZD+PyMi2NoT2Yi2ASVPPMyVbGK3O4TuXtR8+gG5EkM2Yl9BoBQhvuaX4e59JTDJRyAqe4EMSklWqHP5zm8rZh3LInMnofzqBOh82EQ5l/xWUvx56THCGoSuAZAKRy3MXdu0D7FNXikYAoJtbWSrpWW0Q5kjYmDyJ2a11S5+m0bDYyiGsCASIQYSW0M+77vOliFv/OkTi9p4GNaSrcjaf/tsnMhMwikOJEWlXA5sLtsu0TMrOFc5G4oko1DpsKj9/C7Oly6bHQRzh76Bp5S4j99S8SZveb0AMAAA==')

query_params = globals()['params'] if 'params' in globals() else None

import pandas as pd
import base64
import json
import gzip

def decode_table(text)->pd.DataFrame:
    decoded = base64.b64decode(text)
    rawdata = gzip.decompress(decoded)
    data = json.loads(rawdata)
    return pd.DataFrame(data)

result = decode_table(query_text)
result['Value'] = result['Value']*13
print(result)