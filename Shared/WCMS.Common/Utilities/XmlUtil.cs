using System;
using System.Xml;

namespace WCMS.Common.Utilities
{
    public static class XmlUtil
    {
        public static string GetValue(XmlNode node, string key)
        {
            if (node == null || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            XmlNode target = null;

            if (key[0] == '@')
            {
                var attrName = key.Substring(1);
                var attributeNode = node.Attributes?[attrName];
                return attributeNode?.Value ?? string.Empty;
            }

            var directAttribute = node.Attributes?[key];
            if (directAttribute != null)
            {
                return directAttribute.Value ?? string.Empty;
            }

            target = node.SelectSingleNode(key) ?? node[key];
            if (target == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(target.InnerText))
            {
                return target.InnerText.Trim();
            }

            return target.Value ?? string.Empty;
        }
    }
}
