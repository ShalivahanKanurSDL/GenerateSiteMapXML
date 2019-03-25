using System;
using System.Text;
using System.Xml;
using Tridion.ContentManager.Templating;
using Tridion.ContentManager.ContentManagement;
using Tridion.ContentManager.CommunicationManagement;
using Tridion.ContentManager.Templating.Assembly;
using Tridion.ContentManager.Publishing;
using Tridion.ContentManager;
using System.Xml.Linq;
using System.Xml.Serialization;
using Tridion.ContentManager.Security;

namespace GenerateSiteMapXML
{
    public class SearchEngineSiteMap : ITemplate
    {
        private Engine engine;
        private Package package;
        private static readonly TemplatingLogger log = TemplatingLogger.GetLogger(typeof(SearchEngineSiteMap));

        public void Transform(Engine engine, Package package)
        {
            //Initialize
            this.engine = engine;
            this.package = package;

            Page page = null;
            Item pageItem = package.GetByType(ContentType.Page);

            if (pageItem != null)
            {
                page = engine.GetObject(pageItem.GetAsSource().GetValue("ID")) as Page;
            }
            else
            {
                throw new InvalidOperationException("No Page found.  Verify that this template is used with a Page.");
            }

            // Get the publication object
            Publication mainPub = page.ContextRepository as Publication;
            StructureGroup rootSg = mainPub.RootStructureGroup;
            XmlDocument sitemapDoc = GenerateSitemap(rootSg);

            // Put the translate lables component into the package
            package.PushItem(Package.OutputName, package.CreateXmlDocumentItem(ContentType.Xml, sitemapDoc));

        }

        private XmlDocument GenerateSitemap(StructureGroup sg, XmlDocument doc = null)
        {

            //create base doc
            doc = new XmlDocument();
            XmlNode docNode = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.AppendChild(docNode);

            //create root "<urlset>" element and append to the doc.

            XmlElement urlsetNode = doc.CreateElement("urlset");
            urlsetNode.SetAttribute("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9");
            //urlsetNode.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
            doc.AppendChild(urlsetNode);

            // Get the list of child structure groups and pages in this SG

            /*Filter filter = new Filter();
            filter.Conditions["ItemType"] = 68;//pages, structure groups
            filter.BaseColumns = ListBaseColumns.Extended;*/

            OrganizationalItemItemsFilter filterData = new OrganizationalItemItemsFilter(engine.GetSession());
            filterData.ItemTypes = new ItemType[] { ItemType.StructureGroup, ItemType.Page };
            filterData.Recursive = true;
            filterData.BaseColumns = ListBaseColumns.Extended;

            XmlElement childItems = sg.GetListItems(filterData);
            foreach (XmlElement item in childItems.SelectNodes("*"))
            {
                string isPublished = item.GetAttribute("IsPublished");
                string itemType = item.GetAttribute("Type");
                string itemId = item.GetAttribute("ID");
                if (int.Parse(itemType) == (int)ItemType.Page)
                {
                    //only include published pages & exclude index.aspx,default.aspx,.ascx,.json pages                    
                    if (bool.Parse(isPublished))
                    {
                        Page childPage = engine.GetObject(itemId) as Page;
                        if (!ShouldBeExcluded(childPage))
                        {
                            XmlNode urlNode = doc.CreateElement("url");
                            urlsetNode.AppendChild(urlNode);

                            XmlNode locNode = doc.CreateElement("loc");
                            locNode.AppendChild(doc.CreateTextNode(childPage.PublishLocationUrl));
                            urlNode.AppendChild(locNode);

                            XmlNode lastmodNode = doc.CreateElement("lastmod");
                            lastmodNode.AppendChild(doc.CreateTextNode(childPage.RevisionDate.ToString()));
                            urlNode.AppendChild(lastmodNode);

                            XmlNode priorityNode = doc.CreateElement("priority");
                            priorityNode.AppendChild(doc.CreateTextNode("0.5"));
                            urlNode.AppendChild(priorityNode);
                        }
                    }
                }
                else
                {
                    log.Debug("sgId=" + itemId);
                    //if it's a structure group, then get the object, create a child sitemap node
                    StructureGroup childSg = engine.GetObject(itemId) as StructureGroup;
                    if (!ShouldBeExcluded(childSg))
                    {
                        if (!childSg.Title.Contains("_"))
                        {
                            XmlNode urlNode = doc.CreateElement("url");
                            urlsetNode.AppendChild(urlNode);

                            XmlNode locNode = doc.CreateElement("loc");
                            locNode.AppendChild(doc.CreateTextNode(childSg.PublishLocationUrl));
                            urlNode.AppendChild(locNode);

                            XmlNode lastmodNode = doc.CreateElement("lastmod");
                            lastmodNode.AppendChild(doc.CreateTextNode(childSg.RevisionDate.ToString()));
                            urlNode.AppendChild(lastmodNode);

                            XmlNode priorityNode = doc.CreateElement("priority");
                            priorityNode.AppendChild(doc.CreateTextNode("0.5"));
                            urlNode.AppendChild(priorityNode);

                            GenerateSitemap(childSg, doc);
                        }
                    }
                }
            }
            doc.Save(@"C:\Temp\testfolder\testfile.xml");
            return doc;
        }

        private string RemoveNumberPrefix(string p)
        {
            string NUMBER_PREFIX_SEPARATOR = "_";
            return p.Substring(p.IndexOf(NUMBER_PREFIX_SEPARATOR) + 1);
        }

        private bool ShouldBeExcluded(Page page)
        {
            string url = page.PublishLocationUrl;
            string title = page.Title;

            //filter out user controls
            if (url.EndsWith("ascx") || url.EndsWith("sitemap"))
            {
                return true;
            }

            //filter out if index/default aspx,.json pages
            string[] indexPageNames = new string[] { "index.aspx", "default.aspx", ".json" };
            foreach (string indexName in indexPageNames)
                if (url.ToLower().EndsWith(indexName) || url.ToLower().Contains(indexName))
                {
                    return true;
                }

            return false;
        }

        private bool ShouldBeExcluded(StructureGroup sg)
        {

            OrganizationalItemItemsFilter filterData = new OrganizationalItemItemsFilter(engine.GetSession());
            filterData.ItemTypes = new ItemType[] { ItemType.StructureGroup, ItemType.Page };
            filterData.Recursive = true;
            filterData.BaseColumns = ListBaseColumns.Extended;

            int pages = sg.GetListItems(filterData).SelectNodes("*").Count;

            /*
            Filter filter = new Filter();
            filter.Conditions["ItemType"] = ItemType.StructureGroup;
            filter.Conditions["Recursive"] = true;
            filter.BaseColumns = ListBaseColumns.Id;
            int pages = sg.GetListItems(filter).SelectNodes("*").Count;*/
            return pages == 0 ? true : false;
        }
    }
}
