using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MySecondExtension
{
    [Command(PackageIds.MyCommand)]
    internal sealed class MyCommand : BaseCommand<MyCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            //await VS.MessageBox.ShowWarningAsync("MySecondExtension", "Button clicked");
            var docView = await VS.Documents.GetActiveDocumentViewAsync();

            string code = docView.TextBuffer.CurrentSnapshot.GetText();
            List<(string,string)> properties = ExtractProperties(code);

            var selection = docView?.TextView.Selection.SelectedSpans.FirstOrDefault();

            if (selection.HasValue)
            {
                //var guid = Guid.NewGuid().ToString();
                //docView.TextBuffer.Replace(selection.Value, guid);
                var className = selection.Value.GetText();
                if (!string.IsNullOrEmpty(className))
                {
                    var builderClass = GenerateBuilderClass(className, properties);

                    // 将生成的类插入到当前文档中
                    if (!string.IsNullOrEmpty(builderClass))
                    {
                        var textBuffer = docView.TextBuffer;
                        var edit = textBuffer.CreateEdit();
                        var position = textBuffer.CurrentSnapshot.Length; // 插入位置为选中文本的末尾
                        edit.Insert(position, builderClass);
                        edit.Apply();
                    }
                }
            }
        }

        public List<(string type, string name)> ExtractProperties(string code)
        {
            List<(string, string)> properties = new List<(string, string)>();

            // 使用语法分析器解析 C# 代码
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);
            var root = syntaxTree.GetRoot();

            // 查找所有的类声明
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDeclaration in classDeclarations)
            {
                // 获取类的所有属性
                var propertyDeclarations = classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>();
                foreach (var propertyDeclaration in propertyDeclarations)
                {
                    // 获取属性的名称和类型
                    string propertyName = propertyDeclaration.Identifier.ValueText.ToLower();
                    string propertyType = propertyDeclaration.Type.ToString();

                    properties.Add((propertyType,propertyName));
                }
            }

            return properties;
        }



        public string GenerateBuilderClass(string selectedClassName, List<(string type, string name)> properties)
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            string className = selectedClassName + "Builder";
            StringBuilder sb = new StringBuilder();

            // 生成类头部
            sb.AppendLine($"public class {className}");
            sb.AppendLine("{");

            if (properties != null)
            {
                // 生成私有字段
                foreach (var property in properties)
                {
                    sb.AppendLine($"    private {property.type} _{property.name};");
                }
                sb.AppendLine();

                sb.AppendLine($"    public {className}()");
                sb.AppendLine("    {");
                sb.AppendLine("    }");
                sb.AppendLine();

                sb.AppendLine($"    public static {className} Empty => new();");
                sb.AppendLine();

                // 生成属性方法
                foreach (var property in properties)
                {
                    sb.AppendLine($"    public {className} {textInfo.ToTitleCase(property.name)}({property.type} {property.name})");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        _{property.name} = {property.name};");
                    sb.AppendLine("        return this;");
                    sb.AppendLine("    }");
                    sb.AppendLine();
                }

                // 生成Build方法
                sb.AppendLine($"    public {selectedClassName} Build()");
                sb.AppendLine("    {");
                sb.AppendLine($"        return new {selectedClassName}");
                sb.AppendLine("        {");
                
                foreach (var property in properties)
                {
                    sb.AppendLine($"            {textInfo.ToTitleCase(property.name)}  = _{property.name},");
                }
                sb.AppendLine("        };");
                sb.AppendLine("    }");
            }
            else
            {
                // 类型不存在，生成错误信息
                sb.AppendLine($"    // 类型 {selectedClassName} 不存在，请检查类名是否正确。");
            }

            // 生成类结尾
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
