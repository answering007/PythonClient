"""TestScript03."""
import base64
import gzip
import json

import pandas as pd

from typing import Optional


def decode_table(text: str)->pd.DataFrame:
    """
    Decode a base64 encoded string, decompress it using gzip,
    and then load it into a pandas DataFrame using json.loads.
    
    Parameters
    ----------
    text : str
        The string to be decoded.
    
    Returns
    -------
    pd.DataFrame
        The DataFrame containing the data from the string.
    """
    decoded: bytes = base64.b64decode(text)
    raw_data: bytes = gzip.decompress(decoded)
    json_data: dict = json.loads(raw_data)
    return pd.DataFrame(json_data)

query_text: Optional[str] = globals().get('query', None)
if query_text is None:
    raise ValueError("The 'query' variable is not defined in the global scope.")
query_params: Optional[dict] = globals().get('params', None)

result: pd.DataFrame = decode_table(query_text)
result['Value'] = result['Value'] * 13
