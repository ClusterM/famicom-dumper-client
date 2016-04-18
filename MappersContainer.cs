using Cluster.Famicom.Mappers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cluster.Famicom.Mappers
{
    public static class MappersContainer
    {
        public static IMapper[] Mappers = new IMapper[] {
            new NROM(),
            new MMC1(),
            new UxROM(),
            new CNROM(),
            new MMC3(),
            new AxROM(),
            //new Mapper182(),
            //new Multicart9999999(),
            new BMC1024CA1(),
            new Coolboy(),
            new Coolgirl()
            //new MapperTest(),
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
