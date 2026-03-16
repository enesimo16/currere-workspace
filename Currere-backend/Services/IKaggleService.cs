namespace Currere_backend.Services
{
    public interface IKaggleService
    {
        // Kaggle'da dataset araması
        Task<string> SearchDatasetsAsync(int userId, string query);

        // Kaggle'dan veriyi indirir, zip'ten çıkarır ve çalışma alanına kaydetmem
        Task<List<string>> DownloadDatasetAsync(int userId, int workspaceId, string datasetRef);
    }
}