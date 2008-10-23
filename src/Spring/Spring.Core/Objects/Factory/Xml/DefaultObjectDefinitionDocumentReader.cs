#region License

/*
 * Copyright  2002-2005 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

#region Imports

using System;
using System.Globalization;
using System.IO;
using System.Xml;
using Common.Logging;
using Spring.Core.IO;
using Spring.Objects.Factory.Config;
using Spring.Objects.Factory.Support;

#endregion

namespace Spring.Objects.Factory.Xml
{
    /// <summary>
    /// XML resource reader. 
    /// </summary>
    /// <remarks>
    /// <p>
    /// Navigates through an XML resource and invokes parsers registered
    /// with the <see cref="NamespaceParserRegistry"/>.
    /// </p>
    /// </remarks>
    /// <author>Rod Johnson</author>
    /// <author>Juergen Hoeller</author>
    /// <author>Rick Evans (.NET)</author>
    public class DefaultObjectDefinitionDocumentReader : IObjectDefinitionDocumentReader
    {
        #region Constants

        /// <summary>
        /// The shared <see cref="Common.Logging.ILog"/> instance for this class (and derived classes). 
        /// </summary>
        protected static readonly ILog log =
            LogManager.GetLogger(typeof(DefaultObjectDefinitionDocumentReader));

        #endregion

        #region Fields

        private XmlReaderContext readerContext;

        #endregion

        #region Constructor (s) / Destructor

        /// <summary>
        /// Creates a new instance of the DefaultObjectDefinitionDocumentReader class.
        /// </summary>
        public DefaultObjectDefinitionDocumentReader()
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the reader context.
        /// </summary>
        /// <value>The reader context.</value>
        public XmlReaderContext ReaderContext
        {
            get { return readerContext; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Read object definitions from the given DOM element, and register
        /// them with the given object registry.
        /// </summary>
        /// <param name="doc">The DOM element containing object definitions, usually the
        /// root (document) element.</param>
        /// <param name="readerContext">The current context of the reader.  Includes
        /// the resource being parsed</param>
        /// <returns>
        /// The number of object definitions that were loaded.
        /// </returns>
        /// <exception cref="Spring.Objects.ObjectsException">
        /// In case of parsing errors.
        /// </exception>
        public void RegisterObjectDefinitions(XmlDocument doc, XmlReaderContext readerContext)
        {
            //int objectDefinitionCounter = 0;

            this.readerContext = readerContext;

            #region Instrumentation

            if (log.IsDebugEnabled)
            {
                log.Debug("Loading object definitions.");
            }

            #endregion

            XmlElement root = doc.DocumentElement;

            ObjectDefinitionParserHelper parserHelper = CreateHelper(readerContext, root);


            PreProcessXml(root);

            ParseObjectDefinitions(root, parserHelper);

            PostProcessXml(root);

            #region Instrumentation

            if (log.IsDebugEnabled)
            {
                log.Debug(
                    string.Format(
                        "Found {0} <{1}> elements defining objects.",
                        readerContext.Registry.ObjectDefinitionCount,
                        ObjectDefinitionConstants.ObjectElement));
            }

            #endregion
        }

        /// <summary>
        /// Parses object definitions starting at the given <see cref="XmlElement"/> 
        /// using the passed <see cref="ObjectDefinitionParserHelper"/>.
        /// </summary>
        /// <param name="root">The root element to start parsing from.</param>
        /// <param name="helper">The <see cref="ObjectDefinitionParserHelper"/> instance to use.</param>
        /// <exception cref="ObjectDefinitionStoreException">
        /// in case an error happens during parsing and registering object definitions
        /// </exception>
        protected virtual void ParseObjectDefinitions(XmlElement root, ObjectDefinitionParserHelper helper)
        {
            foreach (XmlNode node in root.ChildNodes)
            {
                if (node.NodeType == XmlNodeType.Element)
                {
                    XmlElement element = (XmlElement) node;
                    try
                    {
                        INamespaceParser parser = GetNamespaceParser(element, helper);
                        ParserContext parserContext = new ParserContext(helper.ReaderContext, helper);
                        parser.ParseElement(element, parserContext);
                    }
                    catch( ObjectDefinitionStoreException )
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        helper.ReaderContext.ReportException(node, null, "Failed parsing element", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Loads external XML object definitions from the resource described by the supplied
        /// <paramref name="resource"/>.
        /// </summary>
        /// <param name="resource">The XML element describing the resource.</param>
        /// <exception cref="Spring.Objects.Factory.ObjectDefinitionStoreException">
        /// If the resource could not be imported.
        /// </exception>
        protected virtual void ImportObjectDefinitionResource(XmlElement resource)
        {
            string location = resource.GetAttribute(ObjectDefinitionConstants.ImportResourceAttribute);
            try
            {
                #region Instrumentation

                if (log.IsDebugEnabled)
                {
                    log.Debug(string.Format(
                                  CultureInfo.InvariantCulture,
                                  "Attempting to import object definitions from '{0}'.", location));
                }

                #endregion

                IResource importResource = ReaderContext.Resource.CreateRelative(location);
                ReaderContext.Reader.LoadObjectDefinitions(importResource);
            }
            catch (IOException ex)
            {
                ReaderContext.ReportException(resource, null, string.Format(
                                                                  CultureInfo.InvariantCulture,
                                                                  "Invalid relative resource location '{0}' to import object definitions from.",
                                                                  location), ex);
            }
        }

        /// <summary>
        /// Parses the given alias element, registering the alias with the registry.
        /// </summary>
        /// <param name="aliasElement">The alias element.</param>
        /// <param name="registry">The registry.</param>
        protected virtual void ParseAlias(XmlElement aliasElement, IObjectDefinitionRegistry registry)
        {
            string name = aliasElement.GetAttribute(ObjectDefinitionConstants.NameAttribute);
            string alias = aliasElement.GetAttribute(ObjectDefinitionConstants.AliasAttribute);
            registry.RegisterAlias(name, alias);
        }

        /// <summary>
        /// Parse an object definition and register it with the object factory..
        /// </summary>
        /// <param name="element">The element containing the object definition.</param>
        /// <param name="helper">The helper.</param>
        /// <seealso cref="Spring.Objects.Factory.Support.ObjectDefinitionReaderUtils.RegisterObjectDefinition"/>
        protected virtual void RegisterObjectDefinition(XmlElement element, ObjectDefinitionParserHelper helper)
        {
            ObjectDefinitionHolder holder = null;
            try
            {
                INamespaceParser parser = GetNamespaceParser(element, helper);

                //holder = ParseObjectDefinition(element, parserContext);
                //holder = helper.ParseObjectDefinitionElement(element);
                if (holder == null)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                throw new ObjectDefinitionStoreException(
                    string.Format("Failed parsing object definition '{0}'", element.OuterXml), ex);
            }

            #region Instrumentation

            if (log.IsDebugEnabled)
            {
                log.Debug(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Registering object definition with id '{0}'.", holder.ObjectName));
            }

            #endregion

            ObjectDefinitionReaderUtils.RegisterObjectDefinition(holder, ReaderContext.Registry);
        }

        /// <summary>
        /// <para>
        /// Allow the XML to be extensible by processing any custom element types last,
        /// after we finished processing the objct definitions. This method is a natural
        /// extension point for any other custom post-processing of the XML.
        /// </para><para>
        /// The default implementation is empty. Subclasses can override this method to
        /// convert custom elements into standard Spring object definitions, for example.
        /// Implementors have access to the parser's object definition reader and the
        /// underlying XML resource, through the corresponding properties.
        /// </para>
        /// </summary>
        /// <param name="root">The root.</param>
        protected virtual void PostProcessXml(XmlElement root)
        {
        }

        /// <summary>
        /// Allow the XML to be extensible by processing any custom element types first,
        /// before we start to process the object definitions.
        /// </summary>
        /// <remarks>This method is a natural
        /// extension point for any other custom pre-processing of the XML.
        /// <p>The default implementation is empty. Subclasses can override this method to
        /// convert custom elements into standard Spring object definitions, for example.
        /// Implementors have access to the parser's object definition reader and the
        /// underlying XML resource, through the corresponding properties.
        /// </p>
        /// </remarks>
        /// <param name="root">The root element of the XML document.</param>
        protected virtual void PreProcessXml(XmlElement root)
        {
        }

        /// <summary>
        /// Creates an <see cref="ObjectDefinitionParserHelper"/> instance for the given <paramref name="readerContext"/> and <paramref name="root"/> element.
        /// </summary>
        /// <param name="readerContext">the <see cref="XmlReaderContext"/> to create the <see cref="ObjectDefinitionParserHelper"/> </param>
        /// <param name="root">the root <see cref="XmlElement"/> to start reading from</param>
        /// <returns>a new <see cref="ObjectDefinitionParserHelper"/> instance</returns>
        protected virtual ObjectDefinitionParserHelper CreateHelper(XmlReaderContext readerContext, XmlElement root)
        {
            ObjectDefinitionParserHelper helper = new ObjectDefinitionParserHelper(readerContext);
            helper.InitDefaults(root);
            return helper;
        }

        private INamespaceParser GetNamespaceParser(XmlElement element, ObjectDefinitionParserHelper helper)
        {
            INamespaceParser parser = NamespaceParserRegistry.GetParser(element.NamespaceURI);
            if (parser == null)
            {
                helper.ReaderContext.ReportException(element, null, GetNoParserForNamespaceMessage(element.NamespaceURI));
            }
            return parser;
        }

        private string GetNoParserForNamespaceMessage(string namespaceURI)
        {
            return "There is no parser registered for namespace '" + namespaceURI + "'";
        }

        #endregion
    }
}
