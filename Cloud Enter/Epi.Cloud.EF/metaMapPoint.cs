//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Epi.Data.EF
{
    using System;
    using System.Collections.Generic;
    
    public partial class metaMapPoint
    {
        public int MapPointId { get; set; }
        public string DataSourceTableName { get; set; }
        public string DataSourceXCoordinateColumnName { get; set; }
        public string DataSourceYCoordinateColumnName { get; set; }
        public string DataSourceLabelColumnName { get; set; }
        public int Size { get; set; }
        public int Color { get; set; }
        public int MapId { get; set; }
    
        public virtual metaMap metaMap { get; set; }
    }
}