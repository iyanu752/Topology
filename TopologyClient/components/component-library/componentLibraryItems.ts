export type ComponentLibraryItem = {
  id: string;
  name: string;
  description: string;
  imageSrc?: string;
  activeImageSrc?: string;
  placeholderLabel?: string;
};

export const componentLibraryItems: ComponentLibraryItem[] = [
  {
    id: "client",
    name: "Client",
    description: "Represents users, browsers, mobile apps, or devices sending requests.",
    placeholderLabel: "CL"
  },
  {
    id: "api-gateway",
    name: "API Gateway",
    description: "Routes client requests, handles policies, and centralizes API entry points.",
    placeholderLabel: "AG"
  },
  {
    id: "load-balancer",
    name: "Load Balancer",
    description: "Distributes traffic across healthy services or server instances.",
    placeholderLabel: "LB"
  },
  {
    id: "service",
    name: "Service",
    description: "Runs application logic, business rules, and request handling.",
    placeholderLabel: "SV"
  },
  {
    id: "database",
    name: "Database",
    description: "Stores persistent application data.",
    imageSrc: "/assets/component-library/database/inactive.png",
    activeImageSrc: "/assets/component-library/database/active.png"
  },
  {
    id: "cache",
    name: "Cache",
    description: "Stores frequently accessed data for faster reads and reduced load.",
    placeholderLabel: "CA"
  },
  {
    id: "queue",
    name: "Queue",
    description: "Buffers background work and decouples producers from consumers.",
    placeholderLabel: "QU"
  },
  {
    id: "object-storage",
    name: "Object Storage",
    description: "Stores files, media, backups, logs, and other blob-style data.",
    placeholderLabel: "OS"
  },
  {
    id: "cdn",
    name: "CDN",
    description: "Serves static assets near users to reduce latency and origin load.",
    placeholderLabel: "CD"
  },
  {
    id: "external-api",
    name: "External API",
    description: "Represents third-party services your system depends on.",
    placeholderLabel: "EA"
  }
];
