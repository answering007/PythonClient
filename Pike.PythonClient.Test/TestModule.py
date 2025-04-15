import pandas as pd

def test_function(df: pd.DataFrame) -> pd.DataFrame:
    df['test_function'] = 'test'
    return df