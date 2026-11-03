"""TestScript04: polars result without pandas conversion."""
import base64
import datetime as dt
import gzip
import json

import polars as pl

query_text: str | None = globals().get('query', None)
query_params: dict = globals().get('params', {}) or {}

result: pl.DataFrame = pl.DataFrame({
    'No': pl.Series([1, 2, 3], dtype=pl.Int64),
    'Name': ['Olivia', 'Ava', 'Isabella'],
    'Value': pl.Series([0.0, 0.6931471805599453, 1.0986122886681098], dtype=pl.Float64),
    'Created': pl.Series([dt.date(2020, 1, 1), None, dt.date(1960, 1, 1)], dtype=pl.Date),
    'Flag': pl.Series([True, None, False], dtype=pl.Boolean),
})
