﻿using Newtonsoft.Json;
using PLang.Errors;
using PLang.Errors.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace PLang.Modules.XmlModule
{
	public class Program : BaseProgram
	{

		public record SubElement(Dictionary<string, string>? Attributes = null, string? Value = null);


		[Description("Adds an element to xml document. Can include sub elements. subElements diction object is formatted: {\"Attributes\":{ \"Key\": string, \"Value\": string }, \"Value\":string }")]
		public async Task<(XmlDocument, IError?)> AddElement(XmlDocument xmlDoc, string nameOfElement,
				string insertElementInsideElement, Dictionary<string, string>? attributeOnElement = null,
				Dictionary<string, object>? subElements = null)
		{

			XmlElement newItem = xmlDoc.CreateElement(nameOfElement);
			if (attributeOnElement != null)
			{
				foreach (var attibute in attributeOnElement)
				{
					newItem.Attributes.Append(xmlDoc.CreateAttribute(attibute.Key, attibute.Value));
				}
			}

			XmlNode? node = xmlDoc.SelectSingleNode(insertElementInsideElement);
			if (node == null)
			{
				return (xmlDoc, new ProgramError($"Could not find {insertElementInsideElement} in your xml", goalStep, function));
			}

			if (subElements != null)
			{
				foreach (var subElement in subElements)
				{
					XmlElement item;
					if (subElement.Key.Contains(":"))
					{
						var name = subElement.Key.Split(":");
						XmlElement? root = xmlDoc.DocumentElement;
						string namesp = "urn:dummy-namespace";
						if (root != null)
						{
							namesp = root.GetNamespaceOfPrefix(name[0]);
						}
						
						item = xmlDoc.CreateElement(name[0], name[1], namesp);
					}
					else
					{
						item = xmlDoc.CreateElement(subElement.Key);
					}
					if (subElement.Value != null)
					{
						var subElementValue = JsonConvert.DeserializeObject<SubElement>(subElement.Value.ToString());
						if (subElementValue == null) continue;

						if (subElementValue.Attributes != null)
						{
							foreach (var subAttributes in subElementValue.Attributes)
							{
								var attr = xmlDoc.CreateAttribute(subAttributes.Key);
								attr.InnerText = subAttributes.Value;
								item.Attributes.Append(attr);
							}
						}

						if (subElementValue.Value != null)
						{
							item.InnerText = subElementValue.Value;
						}
					}
					newItem.AppendChild(item); 
				}

			}


			node.AppendChild(newItem);

			return (xmlDoc, null);

		}


	}
}
