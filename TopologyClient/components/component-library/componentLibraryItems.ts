export type ComponentLibraryItem = {
  id: string;
  name: string;
  description: string;
  imageSrc: string;
  activeImageSrc?: string;
};

export const componentLibraryItems: ComponentLibraryItem[] = [
  {
    id: "database",
    name: "Database",
    description: "Stores persistent application data.",
    imageSrc: "/assets/component-library/database/inactive.png",
    activeImageSrc: "/assets/component-library/database/active.png"
  }
];
