import pandas as pd
import numpy as np
import json
import sys
import warnings

# json bozulmasın diye uyarilari gizliyoruz
warnings.filterwarnings('ignore')

def profile_dataset(file_path):
    try:
        df = pd.read_csv(file_path)
        
        # json
        profile = {
            "status": "success",
            "summary": {
                "row_count": int(df.shape[0]),
                "column_count": int(df.shape[1]),
                "memory_usage_mb": round(df.memory_usage(deep=True).sum() / (1024 * 1024), 2)
            },
            "columns": {},
            "correlations": {}
        }
        
        num_cols = df.select_dtypes(include=[np.number]).columns.tolist()
        
        # eda
        for col in df.columns:
            col_data = df[col]
            missing_count = int(col_data.isnull().sum())
            
            col_info = {
                "dtype": str(col_data.dtype),
                "missing_count": missing_count,
                "missing_percentage": round((missing_count / len(df)) * 100, 2),
                "unique_count": int(col_data.nunique())
            }
            
            # dot istatistics
            if col in num_cols:
                col_info["type"] = "numeric"
                if col_info["unique_count"] > 0 and missing_count < len(df):
                    col_info["min"] = float(col_data.min())
                    col_info["max"] = float(col_data.max())
                    col_info["mean"] = round(float(col_data.mean()), 2)
                    
                    # iqr  persentiles
                    q1 = float(col_data.quantile(0.25))
                    q3 = float(col_data.quantile(0.75))
                    col_info["q1"] = q1
                    col_info["q3"] = q3
                    col_info["iqr"] = round(q3 - q1, 2)
                    
                    # skewness
                    col_info["skewness"] = round(float(col_data.skew()), 2)
            else:
                # mode
                col_info["type"] = "categorical"
                if col_info["unique_count"] > 0 and missing_count < len(df):
                    col_info["mode"] = str(col_data.mode()[0])
            
            profile["columns"][col] = col_info
            
        # korelasyon Matrisi
        # sadece +- 0.4
        if len(num_cols) > 1:
            corr_matrix = df[num_cols].corr()
            corr_pairs = {}
            for i in range(len(num_cols)):
                for j in range(i+1, len(num_cols)):
                    val = corr_matrix.iloc[i, j]
                    if pd.notnull(val) and abs(val) > 0.4:
                        pair_name = f"{num_cols[i]}_vs_{num_cols[j]}"
                        corr_pairs[pair_name] = round(float(val), 2)
            profile["correlations"] = corr_pairs
            
        # c# a
        print(json.dumps(profile))

    except Exception as e:
        print(json.dumps({"status": "error", "message": str(e)}))

# C# ' dinamik adlandırılacak
# profile_dataset('/workspace/senin_dosyan.csv')