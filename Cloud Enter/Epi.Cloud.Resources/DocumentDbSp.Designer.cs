﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Epi.Cloud.Resources {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class DocumentDbSp {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal DocumentDbSp() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Epi.Cloud.Resources.DocumentDbSp", typeof(DocumentDbSp).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to function orderBy(filterQuery) {
        ///    // HTTP error codes sent to our callback funciton by DocDB server.
        ///    var ErrorCode = {
        ///        REQUEST_ENTITY_TOO_LARGE: 413,
        ///    }
        ///
        ///    var collection = getContext().getCollection();
        ///    var collectionLink = collection.getSelfLink();
        ///    var result = new Array(); 
        ///    tryQuery({});
        ///
        ///    function tryQuery(options) {
        ///        var isAccepted = (filterQuery &amp;&amp; filterQuery.length) ?
        ///            collection.queryDocuments(collectionLink, filterQuery, options, callback) :
        ///      [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string GetAllRecordsBySurveyID {
            get {
                return ResourceManager.GetString("GetAllRecordsBySurveyID", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to function orderBy(relateParentId,formId,recStatus, orderByFieldName, continuationToken) {
        ///    // HTTP error codes sent to our callback funciton by DocDB server.
        ///    var ErrorCode = {
        ///        REQUEST_ENTITY_TOO_LARGE: 413,
        ///    }    
        ///    var collection = getContext().getCollection();
        ///    var collectionLink = collection.getSelfLink();
        ///    var result = new Array();
        ///     
        ///    if(relateParentId){
        ///            var filterQuery=&quot;SELECT * FROM c where c.RelateParentId=&apos;&quot;+relateParentId+&quot;&apos;                                [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string OrderBy {
            get {
                return ResourceManager.GetString("OrderBy", resourceCulture);
            }
        }
    }
}
