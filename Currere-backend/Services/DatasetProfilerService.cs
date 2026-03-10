using Currere_backend.DTOs;

namespace Currere_backend.Services
{

    // GECİCİ OLARAK SERVERLESS HIZLI KULLANIM İCİN PY KODU C# A GOMULDU
    // SUAN HALİ HAZIRDA CURRERE-EXTENSION KLASORUNDE data_profiller.py KLASORU VARDIR
    // İLERİDE BURASI data_profiller.py'nin İMAJA DAHİL OLMASI İLE GELISTIRILECEK
    public class DatasetProfilerService : IDatasetProfilerService
    {
        private readonly ICodeExecutionService _executionService;

        // asil hedef olan codexecutionservis'i buraya DI ediyoruz
        public DatasetProfilerService(ICodeExecutionService executionService)
        {
            _executionService = executionService;
        }
        // Python Data Profil Algoritması - C# gomulu
        public async Task<string> ProfileDatasetAsync(int workspaceId, string fileName)
        {
            var pythonCode = """
            import pandas as pd
            import numpy as np
            import json
            import warnings

            warnings.filterwarnings('ignore')

            def profile_dataset(file_path):
                try:
                    df = pd.read_csv(file_path, sep=None, engine='python', on_bad_lines='skip', encoding_errors='ignore')
                    
                    profile = {
                        "status": "success",
                        "summary": {
                            "row_count": int(df.shape[0]),
                            "column_count": int(df.shape[1])
                        },
                        "columns": {},
                        "correlations": {}
                    }
                    
                    num_cols = df.select_dtypes(include=[np.number]).columns.tolist()
                    
                    for col in df.columns:
                        col_data = df[col]
                        missing_count = int(col_data.isnull().sum())
                        
                        col_info = {
                            "dtype": str(col_data.dtype),
                            "missing_count": missing_count,
                            "missing_percentage": round((missing_count / len(df)) * 100, 2),
                            "unique_count": int(col_data.nunique())
                        }
                        
                        if col in num_cols:
                            col_info["type"] = "numeric"
                            if col_info["unique_count"] > 0 and missing_count < len(df):
                                col_info["min"] = float(col_data.min())
                                col_info["max"] = float(col_data.max())
                                col_info["mean"] = round(float(col_data.mean()), 2)
                                q1 = float(col_data.quantile(0.25))
                                q3 = float(col_data.quantile(0.75))
                                col_info["iqr"] = round(q3 - q1, 2)
                                col_info["skewness"] = round(float(col_data.skew()), 2)
                        else:
                            col_info["type"] = "categorical"
                            if col_info["unique_count"] > 0 and missing_count < len(df):
                                if not col_data.mode().empty:
                                    col_info["mode"] = str(col_data.mode()[0])
                        
                        profile["columns"][col] = col_info
                        
                    if len(num_cols) > 1:
                        corr_matrix = df[num_cols].corr()
                        for i in range(len(num_cols)):
                            for j in range(i+1, len(num_cols)):
                                val = corr_matrix.iloc[i, j]
                                if pd.notnull(val) and abs(val) > 0.4:
                                    profile["correlations"][f"{num_cols[i]}_vs_{num_cols[j]}"] = round(float(val), 2)
                                    
                    print(json.dumps(profile))

                except Exception as e:
                    print(json.dumps({"status": "error", "message": str(e)}))
            """;

            // dinamik python adi
            pythonCode += $"\n\nprofile_dataset('/workspace/{fileName}')";

            var result = await _executionService.ExecutePythonCodeAsync(workspaceId, pythonCode);

            if (!result.IsSuccess)
            {
                throw new Exception($"Veri profili çıkarılamadı: {result.Error}");
            }

            return result.Output;
        }
    }
}