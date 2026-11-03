"""TestScript02."""
import numpy as np
import pandas as pd
from TestModule import test_function

from typing import Optional

query_text: Optional[str] = globals().get('query', None)
query_params: Optional[dict] = globals().get('params', None)

result: pd.DataFrame = pd.DataFrame({
    'StringColumn':		['Pike',	None,	'Amol'],
    'BoolColumn':		[True,		True,	False],
    'FloatColumn':		[123.456,	np.nan,	456.789],
    'IntColumn':		[123456,	456789,	789123],
    'TimeDeltaColumn':	[np.timedelta64(10, 'h'), None, np.timedelta64(12, 'h')],
    'DateTimeColumn':	[np.datetime64(30, 'Y'), None, np.datetime64(50, 'Y')]
})

result: pd.DataFrame = test_function(result)
