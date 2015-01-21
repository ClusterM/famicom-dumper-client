using Cluster.Famicom.Mappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cluster.Famicom
{
    public static class MappersContainer
    {
        public static IMapper[] Mappers = new IMapper[] {
            new NROM(),
            new MMC1(),
            new UxROM(),
            new CNROM(),
            new MMC3()
        };

        public static IMapper GetMapper(string name)
        {
            foreach (var mapper in Mappers)
            {
                if (mapper.Name.ToLower() == name.ToLower())
                    return mapper;
                if (mapper.Number.ToString() == name)
                    return mapper;
            }
            return null;
        }
    }
}
