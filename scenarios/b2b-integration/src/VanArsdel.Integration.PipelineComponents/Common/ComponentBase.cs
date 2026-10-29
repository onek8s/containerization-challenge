using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.BizTalk.Component.Interop;

namespace VanArsdel.Integration.PipelineComponents.Common
{
    /// <summary>
    /// Design-time plumbing shared by our components: name/version for the pipeline designer, property bag
    /// persistence of the per-pipeline (and per-port, through per-instance configuration) settings.
    /// </summary>
    public abstract class ComponentBase : IBaseComponent, IComponentUI, IPersistPropertyBag
    {
        public abstract string Name { get; }

        public virtual string Version
        {
            get { return "4.0"; }
        }

        public abstract string Description { get; }

        [System.ComponentModel.Browsable(false)]
        public IntPtr Icon
        {
            get { return IntPtr.Zero; }
        }

        public IEnumerator Validate(object projectSystem)
        {
            var errors = ValidateConfiguration();
            return errors == null || errors.Count == 0 ? null : errors.GetEnumerator();
        }

        public void GetClassID(out Guid classID)
        {
            classID = GetType().GUID;
        }

        public void InitNew()
        {
        }

        public void Load(IPropertyBag propertyBag, int errorLog)
        {
            LoadProperties(propertyBag);
        }

        public void Save(IPropertyBag propertyBag, bool clearDirty, bool saveAllProperties)
        {
            SaveProperties(propertyBag);
        }

        protected abstract void LoadProperties(IPropertyBag bag);

        protected abstract void SaveProperties(IPropertyBag bag);

        protected virtual IList<string> ValidateConfiguration()
        {
            return null;
        }

        protected static object ReadProperty(IPropertyBag bag, string name)
        {
            object value;
            try
            {
                bag.Read(name, out value, 0);
            }
            catch (ArgumentException)
            {
                // property not present in an older pipeline or binding
                return null;
            }

            return value;
        }

        protected static string ReadString(IPropertyBag bag, string name, string defaultValue)
        {
            var v = ReadProperty(bag, name);
            return v == null ? defaultValue : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        protected static bool ReadBool(IPropertyBag bag, string name, bool defaultValue)
        {
            var v = ReadProperty(bag, name);
            return v == null ? defaultValue : Convert.ToBoolean(v, CultureInfo.InvariantCulture);
        }

        protected static int ReadInt(IPropertyBag bag, string name, int defaultValue)
        {
            var v = ReadProperty(bag, name);
            return v == null ? defaultValue : Convert.ToInt32(v, CultureInfo.InvariantCulture);
        }

        protected static void WriteProperty(IPropertyBag bag, string name, object value)
        {
            bag.Write(name, ref value);
        }
    }
}
