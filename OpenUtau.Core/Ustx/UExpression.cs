using System;
using System.Diagnostics;
using System.Linq;
using YamlDotNet.Serialization;

namespace OpenUtau.Core.Ustx {
    public enum UExpressionType : int {
        Numerical = 0,
        Options = 1,
        Curve = 2,
    }

    public class UExpressionDescriptor : IEquatable<UExpressionDescriptor> {
        public string name;
        public string abbr; //abbr是表情参数的缩写
        public UExpressionType type;
        public float min; //最小值
        public float max; //最大值
        public float defaultValue; //默认值
        public bool isFlag; //flag是什么？
        public string flag;
        public string[] options;

        /// <summary>
        /// Constructor for Yaml deserialization 用于Yaml反序列化的无参构造函数
        /// </summary>
        public UExpressionDescriptor() { }

        public UExpressionDescriptor(string name, string abbr, float min, float max, float defaultValue, string flag = "") {
            this.name = name;
            this.abbr = abbr.ToLower();
            this.min = min;
            this.max = max;
            this.defaultValue = Math.Min(max, Math.Max(min, defaultValue));
            isFlag = !string.IsNullOrEmpty(flag);
            this.flag = flag;
        }

        public UExpressionDescriptor(string name, string abbr, bool isFlag, string[] options) {
            this.name = name;
            this.abbr = abbr.ToLower();
            type = UExpressionType.Options;
            min = 0;
            max = options.Length - 1;
            this.isFlag = isFlag;
            this.options = options;
        }

        public UExpression Create() {
            return new UExpression(this) {
                value = defaultValue,
            };
        }

        public UExpressionDescriptor Clone() {
            return new UExpressionDescriptor() {
                name = name,
                abbr = abbr,
                type = type,
                min = min,
                max = max,
                defaultValue = defaultValue,
                isFlag = isFlag,
                flag = flag,
                options = (string[])options?.Clone(),
            };
        }

        public override string ToString() => $"{abbr.ToUpper()}: {name}";

        //比较接口的实现
        public bool Equals(UExpressionDescriptor other) {
            return this.name == other.name &&
                this.abbr == other.abbr &&
                this.type == other.type &&
                this.min == other.min &&
                this.max == other.max &&
                this.defaultValue == other.defaultValue &&
                this.isFlag == other.isFlag &&
                this.flag == other.flag &&
                ((this.options == null && other.options == null) || this.options.SequenceEqual(other.options));
        }
    }

    /// <summary>
    /// 表情参数
    /// </summary>
    public class UExpression {
        [YamlIgnore] public UExpressionDescriptor descriptor;

        private float _value;

        //可空类型
        public int? index;
        //abbr是用来干什么的？是表情参数的缩写！
        public string abbr;
        public float value {
            get => _value;
            set => _value = descriptor == null ? value
                : abbr == Format.Ustx.CLR ? value
                : Math.Min(descriptor.max, Math.Max(descriptor.min, value));
        }

        /// <summary>
        /// Constructor for Yaml deserialization
        /// </summary>
        public UExpression() { }

        public UExpression(UExpressionDescriptor descriptor)
        {
            // 确保 descriptor 不为 null
            Trace.Assert(descriptor != null);
            this.descriptor = descriptor; // 设置 descriptor 属性
            abbr = descriptor.abbr; // 设置 abbr 属性为 descriptor 的 abbr
        }

        //只有表情名称的构造函数
        public UExpression(string abbr) {
            this.abbr = abbr;
        }

        //克隆，返回一个新的UExpression对象
        public UExpression Clone() {
            return new UExpression(descriptor) {
                index = index,
                value = value,
            };
        }

        //重写ToString方法
        public override string ToString() => $"{abbr.ToUpper()}: {value}";
    }
}
