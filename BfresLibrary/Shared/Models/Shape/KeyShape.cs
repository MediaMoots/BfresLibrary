using BfresLibrary.Core;

namespace BfresLibrary
{
    public class KeyShape : IResData
    {
        // ---- PROPERTIES ---------------------------------------------------------------------------------------------

        public byte[] TargetAttribIndices { get; set; }

        // ---- METHODS ------------------------------------------------------------------------------------------------

        void IResData.Load(ResFileLoader loader)
        {
            TargetAttribIndices = loader.ReadBytes(20);
        }
        
        void IResData.Save(ResFileSaver saver)
        {
            saver.Write(TargetAttribIndices);
        }
    }
}