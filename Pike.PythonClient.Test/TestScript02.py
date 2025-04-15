query_text = globals()['query'] if 'query' in globals() else None
print("Query text is:", query_text)

query_params = globals()['params'] if 'params' in globals() else None
print("Query parameters:", query_params)

import numpy as np
import pandas as pd

result = pd.DataFrame(
	[['Pike', True, 99.0, 78, np.timedelta64(10, 'h'), np.datetime64(30, 'Y')],
	[None, True, 56.1, 88, np.timedelta64(11, 'h'), np.datetime64(31, 'Y')],
	['Amol', False, 73.2, 45, np.timedelta64(12, 'h'), np.datetime64(40, 'Y')],
	['Lini', False, 69.3, 87, np.timedelta64(13, 'h'), np.datetime64(33, 'Y')]],
	columns=['name', 'physics', 'chemistry','algebra','timedelta', 'datetime'])

from TestModule import test_function

result = test_function(result)