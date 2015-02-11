﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Serialization;
using Schema.Hypermedia.Models;

namespace Schema.Hypermedia
{
    public class HypermediaGenerator
    {
        private Dictionary<string, string> valueCache;
        private const string LeftDelim = "{";
        private const string RightDelim = "}";
        private const string RegExp = LeftDelim + "(.*?)" + RightDelim;
        private JsonSerializer Serializer { get; set; } 

        public HypermediaGenerator()
        {
            Serializer = new JsonSerializer();
            valueCache = new Dictionary<string, string>();
        }

        public HypermediaGenerator(Dictionary<string, string> additionalData)
        {
            this.valueCache = additionalData;
        }

        public HypermediaGenerator(JsonSerializer serializer)
        {
            this.Serializer = serializer;
        }

        public HypermediaGenerator(JsonSerializer serializer, Dictionary<string, string> additionalData)
        {
            this.Serializer = serializer;
            this.valueCache = additionalData;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T">The type of schema and entity</typeparam>
        /// <param name="schema">The schema json in string form</param>
        /// <param name="entity">The entity for which the utility generates links for</param>
        /// <returns></returns>
        public IEnumerable<Link> GetLinks<T>(string schema, T entity) where T : IHypermediaResource
        {
            var parsedSchema = JsonSchema.Parse(schema);
            ValidateEntity(parsedSchema, entity);
            var links = GetLinksFromSchema(schema);
            EnrichLinksWithData(entity, links);
            return links;
        }

        protected internal void EnrichLinksWithData<T>(T entity, IList<Link> links)
        {
            foreach (var link in links)
            {
                // step 1, get matches
                var matches = Regex.Matches(link.Href, RegExp)
                    .Cast<Match>()
                    .Select(m => m.Value)
                    .ToList();
                
                foreach (var match in matches)
                {
                    var replacement = string.Empty;
                    // step 2, check dictionary for matches
                    if (valueCache.ContainsKey(match))
                    {
                        // step 3, if found, get value
                        replacement = valueCache[match];
                    }
                    else
                    {
                        // step 3b. if not, interogate object and get value
                        replacement = GetPropertyValue(match, entity);
                        // step 3b. 2, add to dictionary. 
                        valueCache.Add(match, replacement);
                    }

                    // do the replacement
                    link.Href = link.Href.Replace(match, replacement);
                }
            }
        }


        protected internal string GetPropertyValue(string match, object entity)
        {
            var propName = match.Replace(LeftDelim, string.Empty).Replace(RightDelim, string.Empty);
            var property = entity.GetType()
                .GetProperty(propName,
                    BindingFlags.IgnoreCase |
                    BindingFlags.Public |
                    BindingFlags.Instance);
            if (property == null)
            {
                throw new ArgumentException(string.Format("Unable to find property named: {0}", propName));
            }

            var value = property.GetValue(entity, null);

            if (value == null)
            {
                throw new ArgumentException(string.Format("The value of property {0} is invalid", propName)); 
            }

            return Convert.ToString(value);
        }

        protected internal void ValidateEntity<T>(JsonSchema schema, T entity)
        {
            var jObj = JObject.FromObject(entity, Serializer);
            if (!jObj.IsValid(schema))
            {
                throw new ArgumentException("Entity is not valid for the given scehma.");
            }
        }

        protected internal IList<Link> GetLinksFromSchema(string schema)
        {
            var hyperSchema = JsonConvert.DeserializeObject<HypermediaSchema>(schema);
            return hyperSchema.Links;
        }
    }
}
