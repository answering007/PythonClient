# -*- coding: utf-8 -*-
"""
Created on Sat Nov 21 16:18:40 2020

@author: Pike
"""

query_text = globals()['query'] if 'query' in globals() else None
print("Query text is:", query_text)

query_params = globals()['params'] if 'params' in globals() else None
print("Query parameters:", query_params)

import numpy as np
import pandas as pd

result = pd.DataFrame(
	[['Pike', True, 99.0, 78, np.timedelta64(10, 'h'), np.datetime64(30, 'Y')],
	[None, True, np.nan, 88, None, None],
	['Amol', False, 73.2, 45, np.timedelta64(12, 'h'), np.datetime64(50, 'Y')],
	['Lini', False, 69.3, 87, np.timedelta64(13, 'h'), np.datetime64(60, 'Y')]],
	columns=['name', 'physics', 'chemistry','algebra','timedelta', 'datetime'])
#result['physics'] = result['physics'].astype('bool')
#result['chemistry'] = result['chemistry'].astype('float64')
#result['algebra'] = result['algebra'].astype('int64')