using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;
using Ncqrs.Fluent;

namespace Ncqrs.Config.Autofac.Fluent
{
    public static class INcqrsExtensions
    {
        public static INcqrsConfig WithAutofacConfig(this INcqrs ncqrs, IContainer container)
        {
            return new AutofacConfigurationBuilder(ncqrs, container);
        }
    }
}
