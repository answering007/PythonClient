# -*- coding: utf-8 -*-
"""
Created on Sun Nov 22 10:53:59 2020

@author: Pike
"""
import pandas as pd
import numpy as np

def getColumnNames(x):
    return [str(n) for n in x]

def isDataframe(x):
    return isinstance(x, pd.DataFrame)

def isNoneOrNaN(x):
    return x is None or x != x

def getTypeNo(df, i):
    x = df.iloc[:, i].dtype
    
    if x == np.bool_: return 1
    elif x == np.int8: return 2
    elif x == np.uint8: return 3
    elif x == np.int16: return 4
    elif x == np.uint16: return 5
    elif x == np.uint32: return 7
    elif x == np.int64: return 9
    elif x == np.uint64: return 10
    elif x == np.int32: return 11
    elif x == np.float16: return 12
    elif x == np.float32: return 13
    elif x == np.float64: return 14
    elif x == np.complex128: return 16
    elif x == np.complex64: return 17
    elif x == np.object_: return 19    
    elif str(x).startswith('datetime64'): return 23
    elif str(x).startswith('timedelta64'): return 24
    else: return 0

def dateTimeToTicks(x)->np.int64:
    return np.datetime64(x, 'us').astype(np.int64)*10

def timeDeltaToTicks(x)->np.int64:
    return np.timedelta64(x, 'us').astype(np.int64)*10

def getDataFrameItem(df, r, c):
    return df.iloc[r,c]

def getBoolValue(x)->np.bool_:
    return np.bool(x)

def ticksToDateTime(x):
    return np.datetime64(int(x/10), 'us')