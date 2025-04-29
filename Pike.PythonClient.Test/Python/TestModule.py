"""Module for testing the PythonClient."""
import pandas as pd

def test_function(df: pd.DataFrame) -> pd.DataFrame:
    """
    Add a column named 'test_function' to the DataFrame with the string 'test'.
    
    Parameters
    ----------
    df : pd.DataFrame
        The DataFrame to which the column should be added.
    
    Returns
    -------
    pd.DataFrame
        The DataFrame with the added column.
    """
    df['test_function'] = 'test'
    return df
