﻿using System;
using System.IO;
using System.Collections.Generic;

using UnityEngine;

using KatLib;


namespace CraftManager
{

    public class Tag
    {
        public string name;
        public string save_dir;
        public List<string> craft_list = new List<string>();

        public bool rule_based = false;
        public string rule_attribute = "";
        public string rule_comparitor = "";
        public string rule_value = "";

        public List<string> craft{
            get{ 
                if(rule_based){
                    return rule();
                }
                return craft_list;
            }
        }

        public Tag(string tag_name, string save_name){
            name = tag_name;
            save_dir = save_name;
            Tags.instance.data.Add(this);
        }

        public void add(CraftData craft, bool save = true){
            if(!this.rule_based){
                this.craft.AddUnique(Tags.craft_reference_key(craft));
                craft.tag_name_cache = null;
                if(save){
                    Tags.save();
                }
            }
        }

        public void remove(CraftData craft, bool save = true){
            if(!this.rule_based && this.craft.Contains(Tags.craft_reference_key(craft))){
                this.craft.Remove(Tags.craft_reference_key(craft));
                craft.tag_name_cache = null;
                if(save){
                    Tags.save();
                }
            }
        }

        public bool set_rule(string attr, string comparator, string value){
            bool rule_valid = false;
            if(Tags.instance.rule_attributes.ContainsKey(attr)){
                string attr_type = typeof(CraftData).GetProperty(attr).PropertyType.ToString();
                if(attr_type == "System.String"){
                    if(Tags.instance.rule_comparitors_string.ContainsKey(comparator)){
                        rule_valid = true;
                    }
                } else if(attr_type == "System.Boolean"){
                    if(comparator == "equal_to"){
                        if(value == "True" || value == "False"){
                            rule_valid = true;
                        }
                    }                    
                } else if(attr_type == "System.Int32" || attr_type == "System.Single"){
                    if(Tags.instance.rule_comparitors_numeric.ContainsKey(comparator)){
                        float n;
                        bool is_numeric = float.TryParse(value, out n);
                        if(is_numeric){
                            rule_valid = true;
                        }
                    }
                } 
            }
            if(rule_valid){
                rule_based = true;
                rule_attribute = attr;
                rule_comparitor = comparator;
                rule_value = value;
                return true;
            }else{
                return false;
            }
        }

        public void remove_rule(){
            rule_based = false;
            rule_attribute = "";
            rule_comparitor = "";
            rule_value = "";
        }

        //returns list of craft_references which match the criteria of the tag's rule
        public List<string> rule(){            

            List<string> data = new List<string>();

            List<CraftData> found = CraftData.filtered.FindAll(craft => {
                var c_attr = craft.GetType().GetProperty(rule_attribute).GetValue(craft, null);
                float n;
                bool is_numeric = float.TryParse(c_attr.ToString(), out n);
                if(is_numeric){
                    if(rule_comparitor == "greater_than"){
                        return float.Parse(c_attr.ToString()) > float.Parse(rule_value);
                    }else if(rule_comparitor == "less_than"){
                        return float.Parse(c_attr.ToString()) < float.Parse(rule_value);
                    }else{                        
                        return float.Parse(c_attr.ToString()) == float.Parse(rule_value);
                    }
                }else{
                    if(rule_comparitor == "includes"){
                        return c_attr.ToString().ToLower().Contains(rule_value.ToLower());
                    }else if (rule_comparitor == "starts_with"){
                        return c_attr.ToString().ToLower().StartsWith(rule_value.ToLower());
                    }else{
                        return c_attr.ToString().ToLower() == rule_value.ToLower();
                    }
                }

            });

            foreach(CraftData c in found){
                data.AddUnique(Tags.craft_reference_key(c));
            }
            return data;
        }





    }


    public class Tags
    {

        public List<Tag> data = new List<Tag>(); //Holds all the loaded tags
        public List<string> names_list = new List<string>(); //Holds the naames of Tags (used to draw UI list and dropdown menus).
        public Dictionary<string, bool> selected_lookup = new Dictionary<string, bool>(); //holds a reference to each loaded tag's name and if they are selected or not

        public Dictionary<string, string> rule_attributes = new Dictionary<string, string>{
            {"name", "Name"}, {"crew_capacity", "Crew Capacity"}, {"stock_craft", "Stock"}, {"part_count", "Part Count"}, {"mass_total", "Mass"}
        };
        public Dictionary<string, string> rule_comparitors_numeric = new Dictionary<string, string>{
            {"equal_to", "=="}, {"greater_than", ">"}, {"less_than", "<"}
        };
        public Dictionary<string, string> rule_comparitors_string = new Dictionary<string, string>{
            {"equal_to", "=="}, {"includes", "includes"}, {"starts_with", "starts with"} //even more fancy {"regexp", "regexp"}
        };


        public Tags(){
            Tags.instance = this;  
        }


        //Static Stuff Below

        public static Tags instance;

        //Accessor Methods
        public static List<string> names {
            get{ return Tags.instance.names_list; }
        }

        //return the reference used to identify a craft; ie SPH_myRocket 
        public static string craft_reference_key(CraftData craft){            
            return (craft.stock_craft ? "Stock_" : "") + craft.construction_type + "_" + craft.name;
        }

        //returns the path to the .tags file for a given save dir
        public static string tag_file_path(string save_dir){
            return Paths.joined(CraftManager.ksp_root, "saves", save_dir, "craft.tags");
        }


        //Finds a Tag given a name and save dir
        public static Tag find(string tag_name, string save_dir = CMBrowser.all_saves_ref){
            if(save_dir == CMBrowser.all_saves_ref){
                return Tags.instance.data.Find(t => (t.name == tag_name));
            } else{
                return Tags.instance.data.Find(t => (t.name == tag_name && t.save_dir == save_dir));
            }
        }

        //Find all tags which match a name in either a given save or in all saves if save_dir is given as "<all_saves>"
        public static List<Tag> find_all(string tag_name, string save_dir = CMBrowser.all_saves_ref){
            if(save_dir == CMBrowser.all_saves_ref){
                return Tags.instance.data.FindAll(t => (t.name == tag_name));
            } else{
                return Tags.instance.data.FindAll(t => (t.name == tag_name && t.save_dir == save_dir));
            }                
        }

        //returns a matching Tag or creates a new on if none was fond.
        public static Tag find_or_create_by(string tag_name, string save_dir, bool save_on_create = true){            
            if(String.IsNullOrEmpty(tag_name)){
                return null;
            }
            Tag tag = Tags.find(tag_name, save_dir);
            if(tag == null){
                tag = new Tag(tag_name, save_dir);
            }
            if(save_on_create){
                Tags.save();
            }
            return tag;
        }


        //creates a tag with all attribute options, returns "200" (status ok) if valid. returns error message is not valid
        public static string create(string tag_name, string save_dir, bool rule_based, string rule_attr, string rule_comparator, string rule_value, CraftData craft = null){
            if(String.IsNullOrEmpty(tag_name)){
                return "Tag Name cannot be blank";
            } else if(names.Contains(tag_name)){
                return "A tag with this name already exists";
            } else{
                Tag tag = Tags.find_or_create_by(tag_name, save_dir, false);
                if(rule_based){
                    tag.set_rule(rule_attr, rule_comparator, rule_value);
                } else{
                    if(craft != null){
                        tag.add(craft);
                    }
                }
                Tags.save();
                return "200";
            }
        }
                    
        //updates all tags which match the cur_tag_name and save_dir (which in most cases is a single tag, unless viewing all saves).
        public static string update(string cur_tag_name, string new_tag_name, string save_dir, bool rule_based, string rule_attr, string rule_comparator, string rule_value){
            if(String.IsNullOrEmpty(new_tag_name)){
                return "Name cannot be blank";
            } else if(cur_tag_name != new_tag_name && Tags.names.Contains(new_tag_name)){
                return "A tag with this name already exists";
            } else{                
                List<Tag> tags = Tags.find_all(cur_tag_name, save_dir);                
                foreach(Tag tag in tags){
                    tag.name = new_tag_name;
                    if(rule_based){
                        tag.set_rule(rule_attr, rule_comparator, rule_value);
                    } else{
                        tag.remove_rule();
                    }
                }
                Tags.save();
                if(CraftManager.main_ui){CraftManager.main_ui.refresh();}
                return "200";
            }
        }

        //Remove a Tag from a save or from all saves if save_dir is "<all_save>"
        public static string remove(string tag_name, string save_dir){          
            List<Tag> tags = Tags.find_all(tag_name, save_dir);                
            foreach(Tag tag in tags){
                Tags.instance.data.Remove(tag);
            }
            Tags.save();
            if(CraftManager.main_ui){CraftManager.main_ui.refresh();}
            return "200";
        }

        //Associate a craft with a tag. Will create a Tag with the given name if it doesn't already exist
        public static void tag_craft(CraftData craft, string tag_name){                    
            Tag tag = Tags.find_or_create_by(tag_name, craft.save_dir);
            tag.add(craft);
        }
        //Associate a craft with a set of tags. Creates Tags as needed
        public static void tag_craft(CraftData craft, List<string> tags){
            foreach(string tag_name in tags){
                Tags.tag_craft(craft, tag_name);
            }
        }

        //Unassociate craft with the given tag
        public static void untag_craft(CraftData craft, string tag_name){
            Tag tag = Tags.instance.data.Find(t => (t.name == tag_name && t.save_dir == craft.save_dir));
            if(tag!=null && tag.craft.Contains(craft_reference_key(craft))){                                
                tag.remove(craft);

            }
            Tags.save();
        }
        //Unassociates a craft with all the tags it is associated with. Returns a list of the tags
        public static List<string> untag_craft(CraftData craft){
            List<Tag> tags = Tags.instance.data.FindAll(t => (t.save_dir == craft.save_dir && t.craft.Contains(craft_reference_key(craft))));
            List<string> tag_names = new List<string>();
            foreach(Tag tag in tags){
                tag.remove(craft, false);
                tag_names.AddUnique(tag.name);

            }
            Tags.save();
            return tag_names;
        }

        //get a list of tags for a craft
        public static List<string> for_craft(CraftData craft){
            List<Tag> tags = Tags.instance.data.FindAll(t => (t.save_dir == craft.save_dir && t.craft.Contains(craft_reference_key(craft))));
            List<string> in_tags = new List<string>();
            foreach(Tag tag in tags){
                in_tags.AddUnique(tag.name);
            }
            return in_tags;
        }

        //returns true or false if the given tag_name is selected in the UI
        public static bool is_selected(string tag_name){
            return Tags.instance.selected_lookup[tag_name];
        }
        //Toggles a tags selected status.
        public static void toggle_tag(string tag_name){
            Tags.instance.selected_lookup[tag_name] = !Tags.instance.selected_lookup[tag_name];
        }

        //returns a list of all selected tags
        public static List<string> selected_tags(){
            List<string> s_tags = new List<string>();
            foreach(KeyValuePair<string, bool> tag in Tags.instance.selected_lookup){
                if(tag.Value){
                    s_tags.AddUnique(tag.Key);
                }
            }
            return s_tags;
        }

        //returns the number of craft associated with a given tag name. Takes a second optional argument to specify if the count
        //is for all loaded craft ("<all_saves>"), or limited to the search results ("filtered")
        public static int craft_count_for(string tag_name, string mode = CMBrowser.all_saves_ref){
            List<Tag> tags = Tags.find_all(tag_name);
            int count = 0;
            if(mode == "filtered"){
                foreach(Tag tag in tags){                    
                    count += CraftData.filtered.FindAll(c => tag.save_dir == c.save_dir && tag.craft.Contains(Tags.craft_reference_key(c))).Count;
                }
            } else if(mode == "raw_count"){
                foreach(Tag tag in tags){                    
                    count += tag.craft.Count;
                }
            } else {
                foreach(Tag tag in tags){                    
                    count += CraftData.all_craft.FindAll(c => tag.save_dir==c.save_dir && tag.craft.Contains(Tags.craft_reference_key(c))).Count;
                }
            }
            return count;
        }


        //maintains the two reference lists (selected_lookup and names_list). called after both load and save actions
        //takes the names of all tags and makes a unique list (names_list) and a unique <string, bool> dict (selected_lookup) 
        //but will preserve the state of the coresponding bools in selected_lookup
        public static void update_lists(){
            Dictionary<string, bool> new_list = new Dictionary<string, bool>();
            List<string> new_name_list = new List<string>();

            foreach(Tag tag in Tags.instance.data){
                if(!new_list.ContainsKey(tag.name)){
                    bool cur_val = Tags.instance.selected_lookup.ContainsKey(tag.name) ? Tags.instance.selected_lookup[tag.name] : false;
                    new_list.Add(tag.name, cur_val);
                }
                new_name_list.AddUnique(tag.name);
            }
            Tags.instance.selected_lookup = new_list;
            Tags.instance.names_list = new_name_list;
            if(CraftData.cache != null){
                CraftData.cache.tag_craft_count.Clear();
            }
            foreach(CraftData craft in CraftData.all_craft){
                craft.tag_name_cache = null;
            }
            sort_tag_list();
        }

        public static void sort_tag_list(){
            if(CraftManager.main_ui.tag_sort_by == "craft_count"){
                Tags.instance.names_list.Sort((x,y) => craft_count_for(y).CompareTo(craft_count_for(x)) );
            } else{
                Tags.instance.names_list.Sort((x,y) => x.CompareTo(y));
            }
        }

        //Loads Tag data from confignode file for a given save or for all saves if "<all_saves>" is given
        //If a save does not have a craft.tags file it copies the default.tags file into the save.
        //Will also instanciate an instance of Tags if one does not already exist.
        public static void load(string save_name){
            if(Tags.instance == null){ new Tags(); }
            List<string> save_dirs = new List<string> { save_name };

            Tags.instance.data.Clear();
            if(save_name == CMBrowser.all_saves_ref){
                save_dirs = CraftData.save_names();
            }

            foreach(string save_dir in save_dirs){
                CraftManager.log("loading tags for " + save_dir);
                if(!File.Exists(tag_file_path(save_dir))){
                    FileInfo default_tags = new FileInfo(Paths.joined(CraftManager.ksp_root, "GameData", "CraftManager", "default.tags"));
                    //TODO handle case where default.tags file is not present.
                    default_tags.CopyTo(tag_file_path(save_dir));
                }
                ConfigNode raw_data = ConfigNode.Load(tag_file_path(save_dir));
                ConfigNode tag_nodes = raw_data.GetNode("TAGS");

                foreach(ConfigNode tag_node in tag_nodes.nodes){
                    string tag_name = tag_node.GetValue("tag_name");

                    Tag tag = Tags.find_or_create_by(tag_name, save_dir, false);
                    if(tag_node.HasNode("RULE")){
                        ConfigNode rule_node = tag_node.GetNode("RULE");
                        bool rule_added = tag.set_rule(rule_node.GetValue("attribute"), rule_node.GetValue("comparator"), rule_node.GetValue("value"));
                        if(!rule_added){
                            CraftManager.log("failed to load rule for tag: " + tag_name);
                        }
                    } else{
                        string[] craft = tag_node.GetValues("craft");
                        foreach(string craft_ref in craft){
                            tag.craft.AddUnique(craft_ref);
                        }
                    }

                }
            }
            Tags.update_lists();
            
        }

        //Saves Tag data to config node craft.tags file.  
        public static void save(){
            //First group the tags by the save which they belong to
            Dictionary<string, List<Tag>> tags_by_save = new Dictionary<string, List<Tag>>();
            foreach(Tag tag in Tags.instance.data){
                if(!tags_by_save.ContainsKey(tag.save_dir)){
                    tags_by_save.Add(tag.save_dir, new List<Tag>());
                }
                tags_by_save[tag.save_dir].Add(tag);
            }

            //Then for each group of tags create ConfigNode of data and save to file in coresponding save
            foreach(KeyValuePair<string, List<Tag>> pair in tags_by_save){
                ConfigNode nodes = new ConfigNode();
                ConfigNode tag_nodes = new ConfigNode();
                List<Tag> tags = pair.Value;
                string save_dir = pair.Key;
                foreach(Tag tag in tags){
                    ConfigNode node = new ConfigNode();
                    node.AddValue("tag_name", tag.name);
                    if(tag.rule_based){
                        ConfigNode rule_node = new ConfigNode();
                        rule_node.AddValue("attribute", tag.rule_attribute);
                        rule_node.AddValue("comparator", tag.rule_comparitor);
                        rule_node.AddValue("value", tag.rule_value);
                        node.AddNode("RULE", rule_node);
                    } else{
                        foreach(string craft_ref in tag.craft){
                            node.AddValue("craft", craft_ref);
                        }
                    }
                    tag_nodes.AddNode("TAG", node);
                }
                nodes.AddNode("TAGS", tag_nodes);
                nodes.Save(tag_file_path(save_dir));
            }
            Tags.update_lists();
        }

    }

}

