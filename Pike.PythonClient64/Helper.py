# -*- coding: utf-8 -*-
"""
Created on Sun Nov 22 10:53:59 2020

@author: Pike
"""
import pandas as pd
import numpy as np

def serializeDataFrame(df: pd.DataFrame):
    return df.to_json(orient="table", indent=True, index=False)

def isDataframe(x):
    return isinstance(x, pd.DataFrame)

def getBoolValue(x)->np.bool_:
    return np.bool(x)

def ticksToDateTime(x):
    return np.datetime64(int(x/10), 'us')