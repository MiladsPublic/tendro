import { Flame, Leaf, Martini, Snowflake, type LucideIcon } from 'lucide-react'

export type MenuCatalogItem = {
  id: number
  category: string
  name: string
  price: number
  note: string
  icon: LucideIcon
}

export const menuCatalog: MenuCatalogItem[] = [
  { id: 100, category: 'Grill', name: 'Fireline Burger', price: 16.5, note: 'Double patty, smoked onion jam', icon: Flame },
  { id: 101, category: 'Grill', name: 'Skewer Plate', price: 21, note: 'Chicken, pepper, flatbread', icon: Flame },
  { id: 200, category: 'Salads', name: 'Garden Citrus', price: 10.5, note: 'Orange, fennel, herbs', icon: Leaf },
  { id: 201, category: 'Salads', name: 'Halloumi Crunch', price: 13, note: 'Mint yoghurt dressing', icon: Leaf },
  { id: 300, category: 'Coffee', name: 'Flat White', price: 4.2, note: 'Double ristretto', icon: Martini },
  { id: 301, category: 'Coffee', name: 'Cold Brew Tonic', price: 5.4, note: 'Citrus peel finish', icon: Snowflake },
  { id: 400, category: 'Desserts', name: 'Burnt Cheesecake', price: 8.8, note: 'Sour cherry glaze', icon: Flame },
  { id: 401, category: 'Desserts', name: 'Affogato', price: 6.5, note: 'Vanilla gelato, espresso', icon: Martini },
]