"""TestScript03."""
import base64
import gzip
import json
import pandas as pd

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
    decoded = base64.b64decode(text)
    raw_data = gzip.decompress(decoded)
    json_data = json.loads(raw_data)
    return pd.DataFrame(json_data)

query_text = globals()['query'] if 'query' in globals() else None
query_params = globals()['params'] if 'params' in globals() else None

result = decode_table(query_text)
result['Value'] = result['Value'] * 13
