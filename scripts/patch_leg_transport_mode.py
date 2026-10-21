# -*- coding: utf-8 -*-
# 1. Patch UpsertTransportLegCommand.cs
with open('src/LCMS.Application/TransportLegs/Commands/UpsertTransportLegCommand.cs', 'rb') as f:
    text = f.read().decode('utf-8')

t1 = """    Guid? OriginLocationId = null,
    Guid? DestinationLocationId = null) : IRequest<Guid>;"""

r1 = """    Guid? OriginLocationId = null,
    Guid? DestinationLocationId = null,
    string? TransportMode = null) : IRequest<Guid>;"""

t2 = """                OriginLocationId = request.OriginLocationId,
                DestinationLocationId = request.DestinationLocationId
            };"""

r2 = """                OriginLocationId = request.OriginLocationId,
                DestinationLocationId = request.DestinationLocationId,
                TransportMode = request.TransportMode
            };"""

if t1 not in text:
    t1 = t1.replace('\n', '\r\n')
    r1 = r1.replace('\n', '\r\n')
if t2 not in text:
    t2 = t2.replace('\n', '\r\n')
    r2 = r2.replace('\n', '\r\n')

text = text.replace(t1, r1).replace(t2, r2)
with open('src/LCMS.Application/TransportLegs/Commands/UpsertTransportLegCommand.cs', 'wb') as f:
    f.write(text.encode('utf-8'))
print("Patched UpsertTransportLegCommand.cs")

# 2. Patch OperationalReferenceEndpoints.cs
with open('src/LCMS.Api/Endpoints/OperationalReferenceEndpoints.cs', 'rb') as f:
    text = f.read().decode('utf-8')

t3 = """                    body.OriginLocationId,
                    body.DestinationLocationId),"""

r3 = """                    body.OriginLocationId,
                    body.DestinationLocationId,
                    body.TransportMode),"""

t4 = """    Guid? OriginLocationId = null,
    Guid? DestinationLocationId = null);"""

r4 = """    Guid? OriginLocationId = null,
    Guid? DestinationLocationId = null,
    string? TransportMode = null);"""

if t3 not in text:
    t3 = t3.replace('\n', '\r\n')
    r3 = r3.replace('\n', '\r\n')
if t4 not in text:
    t4 = t4.replace('\n', '\r\n')
    r4 = r4.replace('\n', '\r\n')

text = text.replace(t3, r3).replace(t4, r4)
with open('src/LCMS.Api/Endpoints/OperationalReferenceEndpoints.cs', 'wb') as f:
    f.write(text.encode('utf-8'))
print("Patched OperationalReferenceEndpoints.cs")
