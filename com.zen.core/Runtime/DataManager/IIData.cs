namespace Base
{
    public interface IIData<T> where T : class
    {
        void InitData(T data = null);

        void FillData();

        void FillData(bool showMoreInfo);

        void UpdateData(T data = null);

        void ResetData();
    }
}
